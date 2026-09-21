#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FirebaseAdmin;
using Google.Cloud.Firestore;

/// <summary>
/// SDS registration spine for the admin desktop app.
/// PDF bytes go to OneDrive; Firestore <c>sds/{sdsId}</c> stores queryable metadata
/// plus a pointer/URL so clients can open the file with the OS viewer.
/// </summary>
public static class FirebaseSds
{
    public const string CollectionName = "sds";
    public const string StorageProviderOneDrive = "onedrive";

    private static readonly System.Net.Http.HttpClient Http = new System.Net.Http.HttpClient();

    private static FirestoreDb? _db;
    private static string? _cachedGraphToken;
    private static DateTime _graphTokenExpiresUtc = DateTime.MinValue;

    /// <summary>
    /// Creates one logical SDS: upload PDF to OneDrive, then write Firestore metadata.
    /// Call site for <c>PDFRegisterCore</c>.
    /// </summary>
    public static async Task<SdsRegisterResult> RegisterAsync(SdsRegisterRequest request)
    {
        if (ValidateRegisterRequest(request) is string validationError)
            return SdsRegisterResult.Fail(validationError);

        string sdsId = NewSdsId();
        string originalFileName = Path.GetFileName(request.LocalPdfPath);
        string remoteFileName = $"{sdsId}.pdf";

        OneDriveUploadResult upload;
        try
        {
            upload = await UploadPdfToOneDrive(request.LocalPdfPath, remoteFileName);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SDS OneDrive upload exception: {ex.Message}");
            return SdsRegisterResult.Fail("Could not upload the PDF to OneDrive. Check OneDrive configuration.");
        }

        if (!upload.Ok)
            return SdsRegisterResult.Fail(upload.ErrorMessage ?? "Could not upload the PDF to OneDrive.");

        DateTime nowUtc = DateTime.UtcNow;
        var record = new Dictionary<string, object>
        {
            ["sdsId"] = sdsId,
            ["displayName"] = request.DisplayName.Trim(),
            ["storageProvider"] = StorageProviderOneDrive,
            ["storagePath"] = upload.ItemPath!,
            ["oneDriveItemId"] = upload.ItemId!,
            ["fileUrl"] = upload.WebUrl!,
            ["fileName"] = originalFileName,
            ["contentType"] = "application/pdf",
            ["expiresAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(request.ExpiresAt.Date, DateTimeKind.Utc)),
            ["createdAt"] = Timestamp.FromDateTime(nowUtc),
            ["updatedAt"] = Timestamp.FromDateTime(nowUtc),
            ["active"] = true
        };

        if (NullIfWhiteSpace(request.CasNumber) is string cas)
            record["casNumber"] = cas;
        if (NullIfWhiteSpace(request.HazardSummary) is string hazard)
            record["hazardSummary"] = hazard;
        if (NullIfWhiteSpace(request.CreatedByUid) is string createdBy)
            record["createdBy"] = createdBy;

        try
        {
            FirestoreDb db = GetFirestore();
            await db.Collection(CollectionName).Document(sdsId).SetAsync(record);
        }
        catch (Exception ex)
        {
            GD.PrintErr(
                $"SDS Firestore write failed after OneDrive upload. " +
                $"Orphan OneDrive item id={upload.ItemId}, path={upload.ItemPath}. Error: {ex.Message}");
            return SdsRegisterResult.Fail(
                "PDF uploaded to OneDrive, but saving metadata to Firebase failed. Retry or clean up the orphan file.");
        }

        return SdsRegisterResult.Success(sdsId, upload.WebUrl);
    }

    private static string? ValidateRegisterRequest(SdsRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return "Chemical / product name is required.";

        if (string.IsNullOrWhiteSpace(request.LocalPdfPath))
            return "Select an SDS PDF file.";

        if (!File.Exists(request.LocalPdfPath))
            return "The selected PDF file was not found.";

        if (!string.Equals(Path.GetExtension(request.LocalPdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            return "Only PDF files are allowed.";

        if (request.ExpiresAt == default)
            return "Expiration date is required.";

        return null;
    }

    private static string NewSdsId() => $"sds_{Guid.NewGuid():N}";

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FirestoreDb GetFirestore()
    {
        if (_db != null)
            return _db;

        string? projectId = FirebaseApp.DefaultInstance?.Options?.ProjectId
            ?? System.Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
            ?? System.Environment.GetEnvironmentVariable("GCLOUD_PROJECT")
            ?? System.Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");

        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException(
                "Firebase/Firestore project id is missing. Initialize FirebaseConnect or set FIREBASE_PROJECT_ID.");

        _db = FirestoreDb.Create(projectId);
        return _db;
    }

    private static async Task<OneDriveUploadResult> UploadPdfToOneDrive(string localPdfPath, string remoteFileName)
    {
        string? driveId = System.Environment.GetEnvironmentVariable("ONEDRIVE_DRIVE_ID");
        string? folderId = System.Environment.GetEnvironmentVariable("ONEDRIVE_FOLDER_ID");
        if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(folderId))
        {
            return OneDriveUploadResult.Fail(
                "OneDrive is not configured. Set ONEDRIVE_DRIVE_ID and ONEDRIVE_FOLDER_ID.");
        }

        string accessToken = await GetGraphAccessToken();
        byte[] bytes = await File.ReadAllBytesAsync(localPdfPath);

        string uploadUrl =
            $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{folderId}:/{Uri.EscapeDataString(remoteFileName)}:/content";

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        uploadRequest.Content = new ByteArrayContent(bytes);
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        HttpResponseMessage uploadResponse = await Http.SendAsync(uploadRequest);
        string uploadBody = await uploadResponse.Content.ReadAsStringAsync();
        if (!uploadResponse.IsSuccessStatusCode)
        {
            GD.PrintErr($"OneDrive upload failed: {uploadBody}");
            return OneDriveUploadResult.Fail("OneDrive rejected the PDF upload.");
        }

        var uploaded = JsonSerializer.Deserialize<GraphDriveItem>(uploadBody);
        if (uploaded == null || string.IsNullOrWhiteSpace(uploaded.Id))
            return OneDriveUploadResult.Fail("Unexpected OneDrive upload response.");

        string? webUrl = await TryCreateViewLink(driveId, uploaded.Id, accessToken)
            ?? uploaded.WebUrl;

        if (string.IsNullOrWhiteSpace(webUrl))
            return OneDriveUploadResult.Fail("PDF uploaded, but no openable OneDrive URL was returned.");

        string itemPath = string.IsNullOrWhiteSpace(uploaded.ParentReference?.Path)
            ? $"drives/{driveId}/items/{uploaded.Id}"
            : $"{uploaded.ParentReference.Path}/{remoteFileName}";

        return OneDriveUploadResult.Success(uploaded.Id, itemPath, webUrl);
    }

    private static async Task<string?> TryCreateViewLink(string driveId, string itemId, string accessToken)
    {
        string? configuredScope = System.Environment.GetEnvironmentVariable("ONEDRIVE_LINK_SCOPE");
        string linkScope = string.IsNullOrWhiteSpace(configuredScope) ? "organization" : configuredScope;

        string url = $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{itemId}/createLink";
        using var content = new StringContent(
            JsonSerializer.Serialize(new { type = "view", scope = linkScope }),
            Encoding.UTF8,
            "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        HttpResponseMessage response = await Http.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            GD.PrintErr($"OneDrive createLink failed: {body}");
            return null;
        }

        var parsed = JsonSerializer.Deserialize<GraphCreateLinkResponse>(body);
        return parsed?.Link?.WebUrl;
    }

    private static async Task<string> GetGraphAccessToken()
    {
        if (!string.IsNullOrWhiteSpace(_cachedGraphToken) && DateTime.UtcNow < _graphTokenExpiresUtc)
            return _cachedGraphToken!;

        // Dev override: paste a Graph token (expires quickly). Prefer app credentials below.
        string? manualToken = System.Environment.GetEnvironmentVariable("ONEDRIVE_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(manualToken))
        {
            _cachedGraphToken = manualToken;
            _graphTokenExpiresUtc = DateTime.UtcNow.AddMinutes(30);
            return manualToken;
        }

        string? tenantId = System.Environment.GetEnvironmentVariable("ONEDRIVE_TENANT_ID");
        string? clientId = System.Environment.GetEnvironmentVariable("ONEDRIVE_CLIENT_ID");
        string? clientSecret = System.Environment.GetEnvironmentVariable("ONEDRIVE_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "OneDrive auth missing. Set ONEDRIVE_ACCESS_TOKEN or ONEDRIVE_TENANT_ID / ONEDRIVE_CLIENT_ID / ONEDRIVE_CLIENT_SECRET.");
        }

        string tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        HttpResponseMessage response = await Http.PostAsync(tokenUrl, form);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            GD.PrintErr($"OneDrive token request failed: {body}");
            throw new InvalidOperationException("Could not authenticate to Microsoft Graph for OneDrive.");
        }

        var parsed = JsonSerializer.Deserialize<GraphTokenResponse>(body);
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.AccessToken))
            throw new InvalidOperationException("Unexpected Microsoft Graph token response.");

        int expiresIn = parsed.ExpiresIn > 0 ? parsed.ExpiresIn : 3600;
        _cachedGraphToken = parsed.AccessToken;
        _graphTokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        return parsed.AccessToken;
    }

    private sealed class GraphTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private sealed class GraphDriveItem
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("webUrl")] public string? WebUrl { get; set; }
        [JsonPropertyName("parentReference")] public GraphParentReference? ParentReference { get; set; }
    }

    private sealed class GraphParentReference
    {
        [JsonPropertyName("path")] public string? Path { get; set; }
    }

    private sealed class GraphCreateLinkResponse
    {
        [JsonPropertyName("link")] public GraphLink? Link { get; set; }
    }

    private sealed class GraphLink
    {
        [JsonPropertyName("webUrl")] public string? WebUrl { get; set; }
    }

    private readonly record struct OneDriveUploadResult(
        bool Ok,
        string? ItemId,
        string? ItemPath,
        string? WebUrl,
        string? ErrorMessage)
    {
        public static OneDriveUploadResult Success(string itemId, string itemPath, string webUrl)
            => new(true, itemId, itemPath, webUrl, null);

        public static OneDriveUploadResult Fail(string message)
            => new(false, null, null, null, message);
    }
}

/// <summary>
/// Input for <see cref="FirebaseSds.RegisterAsync"/>. Built by the PDF register UI.
/// </summary>
public readonly record struct SdsRegisterRequest(
    string DisplayName,
    string LocalPdfPath,
    DateTime ExpiresAt,
    string? CasNumber = null,
    string? HazardSummary = null,
    string? CreatedByUid = null);

/// <summary>
/// Outcome of SDS registration. Mirrors <see cref="AuthResult"/> for UI handling.
/// </summary>
public readonly record struct SdsRegisterResult(string? SdsId, string? FileUrl, string? ErrorMessage)
{
    public bool Ok => !string.IsNullOrWhiteSpace(SdsId);

    public static SdsRegisterResult Success(string sdsId, string? fileUrl)
        => new(sdsId, fileUrl, null);

    public static SdsRegisterResult Fail(string message)
        => new(null, null, message);
}

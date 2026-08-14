#nullable enable
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public static class FirebaseAuthenticate
{
    private static readonly System.Net.Http.HttpClient Http = new System.Net.Http.HttpClient();

    private static readonly string ToolkitUrlBase = "https://identitytoolkit.googleapis.com/v1/accounts:";

    private static string GetFireBaseAPIKey()
    {
        string? ApiKey = System.Environment.GetEnvironmentVariable("FIREBASE_WEB_API_KEY");
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            GD.PrintErr("Firebase web api key is missing");
            return "";
        }
        return ApiKey;
    }

    public static Task<AuthResult> SignIn(string email, string password)
        => PostEmailPassword("signInWithPassword", email, password, "Sign-in");

    public static Task<AuthResult> SignUp(string email, string password)
        => PostEmailPassword("signUp", email, password, "Sign-up");

    /// <summary>
    /// Identity Toolkit <c>accounts:update</c>. Requires a current ID token.
    /// </summary>
    public static async Task<AuthResult> ChangePassword(string idToken, string newPassword)
    {
        var posted = await TryPost(
            "update",
            new { idToken, password = newPassword, returnSecureToken = true },
            "Change-password");
        if (!posted.Ok)
            return AuthResult.Fail(posted.FailMessage!);

        var parsed = JsonSerializer.Deserialize<AuthTokenResponse>(posted.Body);
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.LocalId))
            return AuthResult.Fail("Unexpected response from authentication service.");

        return AuthResult.Success(new UserSessionData(
            parsed.LocalId,
            parsed.Email ?? string.Empty,
            parsed.IdToken ?? idToken));
    }

    /// <summary>
    /// Identity Toolkit <c>accounts:sendOobCode</c> with <c>PASSWORD_RESET</c>.
    /// Firebase emails the reset link; it does not set the password by itself.
    /// </summary>
    public static async Task<AuthCommandResult> SendPasswordReset(string email)
    {
        var posted = await TryPost(
            "sendOobCode",
            new { requestType = "PASSWORD_RESET", email },
            "Password-reset");
        if (!posted.Ok)
            return AuthCommandResult.Fail(posted.FailMessage!);

        return AuthCommandResult.Success();
    }

    /// <summary>
    /// Identity Toolkit <c>accounts:resetPassword</c>. Completes a reset from an email <c>oobCode</c>.
    /// </summary>
    public static async Task<AuthCommandResult> ConfirmPasswordReset(string oobCode, string newPassword)
    {
        var posted = await TryPost(
            "resetPassword",
            new { oobCode, newPassword },
            "Confirm-password-reset");
        if (!posted.Ok)
            return AuthCommandResult.Fail(posted.FailMessage!);

        return AuthCommandResult.Success();
    }
    
    private static async Task<AuthResult> PostEmailPassword(
    string endpoint,      // "signInWithPassword" | "signUp"
    string email,
    string password,
    string failLogLabel)
    {
        var posted = await TryPost(
            endpoint,
            new { email, password, returnSecureToken = true },
            failLogLabel);
        if (!posted.Ok)
            return AuthResult.Fail(posted.FailMessage!);

        var parsed = JsonSerializer.Deserialize<AuthTokenResponse>(posted.Body);
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.LocalId))
            return AuthResult.Fail("Unexpected response from authentication service.");

        return AuthResult.Success(new UserSessionData(
            parsed.LocalId,
            parsed.Email ?? email,
            parsed.IdToken));
    }

    private static async Task<(bool Ok, string Body, string? FailMessage)> TryPost(
        string endpoint,
        object payload,
        string failLogLabel)
    {
        string apiKey = GetFireBaseAPIKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "", "Authentication service is not configured.");

        string url = $"{ToolkitUrlBase}{endpoint}?key={apiKey}";
        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        try
        {
            HttpResponseMessage response = await Http.PostAsync(url, content);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                GD.PrintErr($"{failLogLabel} failed: {body}");
                string raw = TryGetFirebaseErrorMessage(body);
                return (false, body, MapFirebaseErrorMessage(raw));
            }
            return (true, body, null);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{failLogLabel} exception: {ex.Message}");
            return (false, "", "Network error. Check your connection and try again.");
        }
    }

    private static string TryGetFirebaseErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            var envelope = JsonSerializer.Deserialize<FirebaseErrorEnvelope>(body);
            return envelope?.Error?.Message?.Trim() ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string NormalizeFirebaseErrorCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        // "WEAK_PASSWORD : Password should be..." → "WEAK_PASSWORD"
        int colon = raw.IndexOf(':');
        string code = colon >= 0 ? raw[..colon] : raw;
        return code.Trim().ToUpperInvariant();
    }

    private static string MapFirebaseErrorMessage(string rawMessage)
    {
        string code = NormalizeFirebaseErrorCode(rawMessage);

        return code switch
        {
            "EMAIL_EXISTS" =>
                "This email is already registered.",
            "EMAIL_NOT_FOUND" =>
                "No account exists with this email.",
            "INVALID_PASSWORD" =>
                "Incorrect password.",
            "INVALID_LOGIN_CREDENTIALS" =>
                "Incorrect email or password.",
            "INVALID_EMAIL" =>
                "The email address is invalid.",
            "WEAK_PASSWORD" =>
                "Password is too weak. Use at least 6 characters.",
            "OPERATION_NOT_ALLOWED" =>
                "Email/password accounts are disabled for this project.",
            "USER_DISABLED" =>
                "This account has been disabled.",
            "TOO_MANY_ATTEMPTS_TRY_LATER" =>
                "Too many attempts. Try again later.",
            "MISSING_PASSWORD" =>
                "Password is required.",
            "MISSING_EMAIL" =>
                "Email is required.",
            "INVALID_ID_TOKEN" or "TOKEN_EXPIRED" =>
                "Your session expired. Sign in again and retry.",
            "CREDENTIAL_TOO_OLD_LOGIN_AGAIN" =>
                "Please sign in again before changing your password.",
            "RESET_PASSWORD_EXCEED_LIMIT" =>
                "Too many password reset attempts. Try again later.",
            "INVALID_OOB_CODE" =>
                "This password reset link is invalid.",
            "EXPIRED_OOB_CODE" =>
                "This password reset link has expired.",
            "USER_NOT_FOUND" =>
                "No account exists with this email.",
            _ =>
                string.IsNullOrEmpty(code)
                    ? "Authentication failed. Please try again."
                    : $"Authentication failed ({code})."
        };
    }

    private sealed class AuthTokenResponse
    {
        [JsonPropertyName("localId")] public string? LocalId { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("idToken")] public string? IdToken { get; set; }
    }

    private sealed class FirebaseErrorEnvelope
    {
        [JsonPropertyName("error")]
        public FirebaseErrorBody? Error { get; set; }
    }

    private sealed class FirebaseErrorBody
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}

public readonly record struct AuthResult(UserSessionData? Data, string? ErrorMessage)
{
    public bool Ok => Data is not null;
    public static AuthResult Success(UserSessionData data) => new(data, null);
    public static AuthResult Fail(string message) => new(null, message);
}

public readonly record struct AuthCommandResult(bool Ok, string? ErrorMessage)
{
    public static AuthCommandResult Success() => new(true, null);
    public static AuthCommandResult Fail(string message) => new(false, message);
}
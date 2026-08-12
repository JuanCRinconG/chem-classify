using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public static class FirebaseAuthenticate
{
    private static readonly System.Net.Http.HttpClient Http = new System.Net.Http.HttpClient();
    public static async Task<UserSessionData?> SignIn(string email, string password)
    {
        string apiKey = System.Environment.GetEnvironmentVariable("FIREBASE_WEB_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            GD.PrintErr("Firebase web api key is missing");
            return null;
        }
        string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";
        var payload = new { email, password, returnSecureToken = true };
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
                GD.PrintErr($"Sign-in failed: {body}");
                return null;
            }
            var parsed = JsonSerializer.Deserialize<SignInResponse>(body);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.LocalId))
                return null;
            return new UserSessionData(
                parsed.LocalId,
                parsed.Email ?? email,
                parsed.IdToken);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Sign-in exception: {ex.Message}");
            return null;
        }
    }
    private sealed class SignInResponse
    {
        [JsonPropertyName("localId")] public string LocalId { get; set; }
        [JsonPropertyName("email")] public string Email { get; set; }
        [JsonPropertyName("idToken")] public string IdToken { get; set; }
    }
}

public static class FirebaseCreateUser
{
    
}

/*  

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class FirebaseBackendService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    
    // Store your Firebase Web API key safely in server environment variables
    private readonly string _firebaseApiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY");

    public async Task<CleanBackendResponse> LoginUserAsync(string email, string password)
    {
        // 1. Prepare the standard Firebase REST Auth URL
        string url = $"https://googleapis.com{_firebaseApiKey}";

        // 2. Format the payload exactly how Firebase expects it
        var payload = new
        {
            email = email,
            password = password,
            returnSecureToken = true
        };

        string jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            // 3. Forward the request safely from your server to Google
            HttpResponseMessage response = await _httpClient.PostAsync(url, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Handle wrong password/email errors cleanly here
                return new CleanBackendResponse { Success = false, ErrorMessage = "Invalid credentials." };
            }

            // 4. Parse Google's complex response internally
            var firebaseData = JsonSerializer.Deserialize<FirebaseIdTokenResponse>(responseString);

            // 5. Return ONLY clean, sanitized data back to the Godot Game client
            return new CleanBackendResponse
            {
                Success = true,
                LocalId = firebaseData.LocalId,
                Token = firebaseData.IdToken // Godot will use this token for future database requests
            };
        }
        catch (Exception ex)
        {
            return new CleanBackendResponse { Success = false, ErrorMessage = $"Server error: {ex.Message}" };
        }
    }
}

// DTO to match Google's raw JSON layout internally
public class FirebaseIdTokenResponse
{
    [JsonPropertyName("idToken")] public string IdToken { get; set; }
    [JsonPropertyName("localId")] public string LocalId { get; set; }
}

// Clean DTO sent back to your Godot App UI
public class CleanBackendResponse
{
    public bool Success { get; set; }
    public string LocalId { get; set; }
    public string Token { get; set; }
    public string ErrorMessage { get; set; }
}

*/
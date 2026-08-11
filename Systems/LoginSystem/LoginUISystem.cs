/*
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public partial class LoginUiController : Control
{
    // UI Node References
    [Export] private LineEdit _emailInput;
    [Export] private LineEdit _passwordInput;
    [Export] private Label _statusLabel;

    private static readonly HttpClient _client = new HttpClient();
    private const string BackendUrl = "https://your-custom-backend.com";

    // Triggered by your UI Button's "pressed" signal
    public async void OnLoginButtonPressed()
    {
        string email = _emailInput.Text.Trim();
        string password = _passwordInput.Text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            _statusLabel.Text = "Please fill in all fields.";
            return;
        }

        _statusLabel.Text = "Connecting to game servers...";
        
        // Disable UI elements to prevent double-clicking
        _emailInput.Editable = false;
        _passwordInput.Editable = false;

        CleanBackendResponse result = await RequestLoginFromBackend(email, password);

        // Re-enable UI
        _emailInput.Editable = true;
        _passwordInput.Editable = true;

        if (result != null && result.Success)
        {
            _statusLabel.Text = "Login successful!";
            // Save result.Token globally to attach to your future data requests
            GD.Print($"User Logged In. UID: {result.LocalId}");
            
            // Proceed to the main application or character select screen
        }
        else
        {
            _statusLabel.Text = result?.ErrorMessage ?? "Connection failed.";
        }
    }

    private async Task<CleanBackendResponse> RequestLoginFromBackend(string email, string password)
    {
        var loginData = new { email = email, password = password };
        string json = JsonSerializer.Serialize(loginData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await _client.PostAsync(BackendUrl, content);
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CleanBackendResponse>(jsonResponse, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
        }
        catch (HttpRequestException e)
        {
            GD.PrintErr($"Network Error: {e.Message}");
        }

        return null;
    }
}

// Match the clean structural mapping from the backend
public class CleanBackendResponse
{
    public bool Success { get; set; }
    public string LocalId { get; set; }
    public string Token { get; set; }
    public string ErrorMessage { get; set; }
}
*/
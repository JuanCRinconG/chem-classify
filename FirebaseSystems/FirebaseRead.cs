/*
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

public partial class DatabaseManager : Node
{
    private static readonly HttpClient client = new HttpClient();

    public async Task SendDataToBackend(string userJwtToken, string payloadJson)
    {
        // Your secure Cloud Function URL
        string url = "https://cloudfunctions.net";

        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            // Attach the secure user token for the server to verify
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userJwtToken);
            request.Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                GD.Print("Data securely saved via backend!");
            }
        }
    }
}
*/
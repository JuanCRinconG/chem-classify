using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

public static class FirebaseConnect 
{
    public static void InitializeFirebase()
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.GetApplicationDefault()
            });
        }
    }
}
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;

public partial class FirebaseConnect : Node
{
    public override void _EnterTree()
    {
        InitializeFirebase();
    }
    public override void _Ready()
    {
        _ = TestUsers();
    }

    public void InitializeFirebase()
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.GetApplicationDefault()
            });
        }
    }

    public async Task TestUsers()
    {
        var userRecords = FirebaseAuth.DefaultInstance.ListUsersAsync(null);
        await foreach (var user in userRecords)
        {
            GD.Print($"Connected, sample uid: {user.Uid}");
            break;
        }
        GD.Print("Firebase admin connection seems ok");
    }
}
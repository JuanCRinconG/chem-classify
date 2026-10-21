using System.Threading.Tasks;
using Godot;

public enum AuthPath
{
    None,
    Login,
    CreateAccount
}

public class AuthBoard
{
    public AuthPath Path;
    public string Email;
    public string Password;
    public UserSessionData? Session;
    public string LastError;

    public void ClearSecrets()
    {
        Password = null;
        Session = null;
    }

    public void Reset()
    {
        Path = AuthPath.None;
        Email = null;
        LastError = null;
        ClearSecrets();
    }

    public string ConsumeLastError()
    {
        string error = LastError;
        LastError = null;
        return error;
    }

    public async Task FinishAuth(Control from)
    {
        AppManager app = AppManager.Current;
        switch (Path)
        {
            case AuthPath.Login:
                if (Session is not UserSessionData session)
                {
                    GD.PrintErr("FinishAuth: login path has no pending session");
                    app.SwapTo(from, app.LoginDisplay);
                    return;
                }
                EnterMainApp(from, session);
                break;

            case AuthPath.CreateAccount:
                AuthResult signedUp = await FirebaseAuthenticate.SignUp(Email, Password);
                if (!signedUp.Ok)
                {
                    LastError = signedUp.ErrorMessage ?? "Could not create the account";
                    ClearSecrets();
                    app.SwapTo(from, app.CreateAccountDisplay);
                    return;
                }
                EnterMainApp(from, signedUp.Data.Value);
                break;

            default:
                GD.PrintErr("FinishAuth: no auth path on the board");
                app.SwapTo(from, app.LoginDisplay);
                break;
        }
    }

    public void CancelAuth(Control from)
    {
        Session = null;
        AppManager app = AppManager.Current;
        StringName destination = Path == AuthPath.CreateAccount
            ? app.CreateAccountDisplay
            : app.LoginDisplay;
        app.SwapTo(from, destination);
    }

    public void EnterMainApp(Control from, UserSessionData session)
    {
        _ = new UserSession(session);
        Reset();
        AppManager.Current.SwapTo(from, AppManager.Current.MainAppDisplay);
    }
}

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
}

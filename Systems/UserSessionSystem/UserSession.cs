public class UserSession
{
    public static UserSession Current;

    public string Uid;

    public string Email;

    public string IdToken;

    public UserSession(UserSessionData data)
    {
        Uid = data.Uid;
        Email = data.Email;
        IdToken = data.IdToken;
        Current = this;
    }
}

public readonly record struct UserSessionData
(
    string Uid,
    string Email,
    string IdToken 
    /*
    , string RefreshToken,
    int ExpiresIn
    */
);
using System;
using System.Threading.Tasks;

public partial class LoginUISystem : Control
{
    private UIBindings _LoginBindings = new();

    [Export]
    public LineEdit EmailInput;

    [Export]
    public LineEdit PasswordInput;

    [Export]
    public Button ConfirmFieldsButton;

    [Export]
    public ErrorData LoginError;

    public event Action LoginSuccess;

    public override void _Ready()
    {
        _LoginBindings.BindButton(ConfirmFieldsButton, OnConfirmFields);
    }

    public override void _ExitTree()
    {
        _LoginBindings.Clear();
    }

    public async void OnConfirmFields()
    {
        ConfirmFieldsButton.Disabled = true;
        await VerifyUserInputs();
        ConfirmFieldsButton.Disabled = false;
    }

    public async Task VerifyUserInputs()
    {
        string UserEmail = EmailInput.Text.Trim();
        string MailResult = LoginValidationService.ValidateEmail(UserEmail);
        if (MailResult != LoginValidationService.SuccessFlag)
        {
            LoginError.WarnText = MailResult;
            ErrorService.Current.CastErrorMessage(this, LoginError);
            return;
        }
        string UserPassword = PasswordInput.Text; 
        string PasswordResult = LoginValidationService.ValidatePassword(UserPassword);
        if (PasswordResult != LoginValidationService.SuccessFlag)
        {
            LoginError.WarnText = PasswordResult;
            ErrorService.Current.CastErrorMessage(this, LoginError);
            return;
        }

        UserSessionData? data = await FirebaseAuthenticate.SignIn(UserEmail, UserPassword);
        if (data == null)
        {
            LoginError.WarnText = "Authentication failed, verify that email/password are correct";
            ErrorService.Current.CastErrorMessage(this, LoginError);
            return;
        }
        _ = new UserSession(data.Value);
        LoginSuccess?.Invoke();
    }
}
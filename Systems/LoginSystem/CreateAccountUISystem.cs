using System;
using System.Threading.Tasks;

public partial class CreateAccountUISystem : Control
{
    private UIBindings _LoginBindings = new();

    [Export]
    public LineEdit EmailInput;

    [Export]
    public LineEdit PasswordInput;

    [Export]
    public LineEdit ConfirmPassword;

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
        string MailResult = LoginValidation.Email(UserEmail);
        if (LoginValidation.Invalid(MailResult))
        {
            LoginError.WarnText = MailResult;
            ErrorService.Current.CastErrorMessage(this, LoginError);
            return;
        }
        string UserPassword = PasswordInput.Text; 
        string PasswordResult = LoginValidation.PasswordForCreate(UserPassword);
        if (LoginValidation.Invalid(PasswordResult))
        {
            LoginError.WarnText = PasswordResult;
            ErrorService.Current.CastErrorMessage(this, LoginError);
            return;
        }
        string ConfirmationResult = LoginValidation.PasswordConfirmation(UserPassword, ConfirmPassword.Text);
        if (LoginValidation.Invalid(ConfirmationResult))
        {
            LoginError.WarnText = ConfirmationResult;
            ErrorService.Current.CastErrorMessage(this, LoginError);
            return;
        }

        AuthResult result = await FirebaseAuthenticate.SignUp(UserEmail, UserPassword);
        if (!result.Ok)
        {
            LoginError.WarnText = result.ErrorMessage ?? "Authentication fail, verify that email or password are correct";
            ErrorService.Current.CastErrorMessage(this, LoginError);
            return;
        }
        _ = new UserSession(result.Data.Value);
        LoginSuccess?.Invoke();
    }
}
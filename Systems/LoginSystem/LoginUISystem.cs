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

    [Export]
    public Button ForgotPasswordButton;

    public event Action LoginSuccess;

    public override void _Ready()
    {
        _LoginBindings.BindButton(ConfirmFieldsButton, OnConfirmFields);
        _LoginBindings.BindButton(ForgotPasswordButton, OnForgotPassword);
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

    public void OnForgotPassword()
    {
        ForgotPasswordButton.Disabled = true;
        string UserEmail = EmailInput.Text.Trim();
        if (LoginValidation.Email(UserEmail) is string emailError)
        {
            LoginError.Show(emailError, this);
            ForgotPasswordButton.Disabled = false;
            return;
        }
        ForgotPasswordButton.Disabled = false;
        UISwapService.SwapScenePath(this, UIScreens.Current.TwoFA, node =>
        {
            if (node is TwoFactorAuthenticator auth)
                auth.ReceiverMail = UserEmail;
        });
        
    }

    public async Task VerifyUserInputs()
    {
        string UserEmail = EmailInput.Text.Trim();
        if (LoginValidation.Email(UserEmail) is string emailError)
        {
            LoginError.Show(emailError, this);
            return;
        }

        string UserPassword = PasswordInput.Text;
        if (LoginValidation.Password(UserPassword) is string passwordError)
        {
            LoginError.Show(passwordError, this);
            return;
        }  
        
        AuthResult result = await FirebaseAuthenticate.SignIn(UserEmail, UserPassword);
        if (!result.Ok)
        {
            string authError = result.ErrorMessage ?? "Authentication failed, verify that email or password are correct";
            LoginError.Show(authError, this);
            return;
        }

        _ = new UserSession(result.Data.Value);
        LoginSuccess?.Invoke();
    }
}
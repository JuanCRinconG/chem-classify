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
        if (LoginValidation.Email(UserEmail) is string emailError)
        {
            LoginError.Show(emailError, this);
            return;
        }

        string UserPassword = PasswordInput.Text;
        if (LoginValidation.PasswordForCreate(UserPassword) is string passwordError)
        {
            LoginError.Show(passwordError, this);
            return;
        } 

        if (LoginValidation.PasswordConfirmation(UserPassword, ConfirmPassword.Text) is string confirmationError)
        {
            LoginError.Show(confirmationError, this);
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
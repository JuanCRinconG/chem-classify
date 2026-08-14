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

    public override void _Ready()
    {
        _LoginBindings.BindButton(ConfirmFieldsButton, OnConfirmFields);
        _LoginBindings.BindButton(ForgotPasswordButton, OnForgotPassword);
        RestoreBoardState();
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

    public async void OnForgotPassword()
    {
        ForgotPasswordButton.Disabled = true;

        string UserEmail = EmailInput.Text.Trim();
        if (LoginValidation.Email(UserEmail) is string emailError)
        {
            LoginError.Show(emailError, this);
            ForgotPasswordButton.Disabled = false;
            return;
        }

        AuthCommandResult reset = await FirebaseAuthenticate.SendPasswordReset(UserEmail);
        if (!reset.Ok)
        {
            LoginError.Show(reset.ErrorMessage ?? "Could not send the password reset email", this);
            ForgotPasswordButton.Disabled = false;
            return;
        }

        LoginError.Show("Check your email for the password reset link", this);
        ForgotPasswordButton.Disabled = false;
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

        AuthBoard board = MainUISystem.Current.Board;
        board.Path = AuthPath.Login;
        board.Email = UserEmail;
        board.Password = null;
        board.Session = result.Data;
        MainUISystem.Current.SwapTo(this, UIScreens.Current.TwoFA);
    }

    private void RestoreBoardState()
    {
        AuthBoard board = MainUISystem.Current.Board;
        if (!string.IsNullOrEmpty(board.Email))
        {
            EmailInput.Text = board.Email;
        }

        string lastError = board.ConsumeLastError();
        if (!string.IsNullOrEmpty(lastError))
        {
            LoginError.Show(lastError, this);
        }
    }
}

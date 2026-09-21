public partial class CreateAccountCore : Control
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

    public override void _Ready()
    {
        _LoginBindings.BindButton(ConfirmFieldsButton, OnConfirmFields);
        RestoreBoardState();
    }

    public override void _ExitTree()
    {
        _LoginBindings.Clear();
    }

    public void OnConfirmFields()
    {
        VerifyUserInputs();
    }

    public void VerifyUserInputs()
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

        AuthBoard board = DisplayEngineCore.Current.Board;
        board.Path = AuthPath.CreateAccount;
        board.Email = UserEmail;
        board.Password = UserPassword;
        board.Session = null;
        DisplayEngineCore.Current.SwapTo(this, UIScreens.Current.TwoFA);
    }

    private void RestoreBoardState()
    {
        AuthBoard board = DisplayEngineCore.Current.Board;
        if (!string.IsNullOrEmpty(board.Email))
        {
            EmailInput.Text = board.Email;
        }
        if (!string.IsNullOrEmpty(board.Password))
        {
            PasswordInput.Text = board.Password;
            ConfirmPassword.Text = board.Password;
        }

        string lastError = board.ConsumeLastError();
        if (!string.IsNullOrEmpty(lastError))
        {
            LoginError.Show(lastError, this);
        }
    }
}

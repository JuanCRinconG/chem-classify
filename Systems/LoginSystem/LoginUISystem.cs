using System;

public partial class LoginUISystem : Control
{
    private UIBindings _LoginBindings = new();

    [Export]
    public LineEdit EmailInput;

    [Export]
    public LineEdit PasswordInput;

    [Export]
    public Button ConfirmFieldsButton;

    public event Action LoginSuccess;

    public override void _Ready()
    {
        _LoginBindings.BindButton(ConfirmFieldsButton, OnConfirmFields);
    }

    public async void OnConfirmFields()
    {
        ConfirmFieldsButton.Disabled = true;
        string UserEmail = EmailInput.Text.Trim();
        string UserPassword = PasswordInput.Text; 
        UserSessionData? data = await FirebaseAuthenticate.SignIn(UserEmail, UserPassword);
        if (data == null)
        {
            return;
        }
        _ = new UserSession(data.Value);
        LoginSuccess?.Invoke();
        ConfirmFieldsButton.Disabled = false;
    }
}
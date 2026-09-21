using Godot;
using System.Threading.Tasks;

public partial class DisplayEngineCore : Control
{
	[Export]
	public Control UIContainerSpace;

	[Export]
	public PackedScene FirstUIPanel;

	public static DisplayEngineCore Current;

	public AuthBoard Board { get; } = new();
	
	public override void _EnterTree()
	{
		Current = this;
		FirebaseConnect.InitializeFirebase();
	}

	public override void _Ready()
	{
		if (FirstUIPanel == null)
		{
			GD.Print("First UI Panel is null, can't instance it");
			return;
		}
		var FirstScene = FirstUIPanel.Instantiate();
		UIContainerSpace.AddChild(FirstScene);
	}
	
	public override void _ExitTree()
	{
		Current = null;
	}

	public void SwapTo(Control from, string scenePath)
	{
		UISwapService.SwapScenePath(from, scenePath);
	}

	public async Task FinishAuth(Control from)
	{
		switch (Board.Path)
		{
			case AuthPath.Login:
				if (Board.Session is not UserSessionData session)
				{
					GD.PrintErr("FinishAuth: login path has no pending session");
					SwapTo(from, UIScreens.Current.LoginScreen);
					return;
				}
				EnterMainApp(from, session);
				break;

			case AuthPath.CreateAccount:
				AuthResult signedUp = await FirebaseAuthenticate.SignUp(Board.Email, Board.Password);
				if (!signedUp.Ok)
				{
					Board.LastError = signedUp.ErrorMessage ?? "Could not create the account";
					Board.ClearSecrets();
					SwapTo(from, UIScreens.Current.CreateAccountScreen);
					return;
				}
				EnterMainApp(from, signedUp.Data.Value);
				break;

			default:
				GD.PrintErr("FinishAuth: no auth path on the board");
				SwapTo(from, UIScreens.Current.LoginScreen);
				break;
		}
	}

	public void CancelAuth(Control from)
	{
		Board.Session = null;
		string destination = Board.Path == AuthPath.CreateAccount
			? UIScreens.Current.CreateAccountScreen
			: UIScreens.Current.LoginScreen;
		SwapTo(from, destination);
	}

	public void EnterMainApp(Control from, UserSessionData session)
	{
		_ = new UserSession(session);
		Board.Reset();
		SwapTo(from, UIScreens.Current.MainApp);
	}
}

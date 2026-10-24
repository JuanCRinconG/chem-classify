using Godot;

public partial class AppManager
{
	public AuthBoard Board { get; } = new();

	[Export]
	public DisplayName LoginDisplay;

	[Export]
	public DisplayName CreateAccountDisplay;

	[Export]
	public DisplayName MainAppDisplay;

	[Export]
	public DisplayName TwoFactorDisplay;
}

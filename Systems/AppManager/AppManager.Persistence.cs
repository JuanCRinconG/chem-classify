using Godot;

public partial class AppManager
{
	public AuthBoard Board { get; } = new();

	[Export(PropertyHint.None, DisplayList.GetDisplay)]
	public StringName LoginDisplay;

	[Export(PropertyHint.None, DisplayList.GetDisplay)]
	public StringName CreateAccountDisplay;

	[Export(PropertyHint.None, DisplayList.GetDisplay)]
	public StringName MainAppDisplay;
}

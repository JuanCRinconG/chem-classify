using Godot;
using System;

public partial class AppManager : Node
{

	[Export]
	public Control UIContainerSpace;

	[Export]
	public DisplayName InitialDisplay;

	[Export]
	public DisplayList Displays;

	public static AppManager Current;
	
	public override void _EnterTree()
	{
		Current = this;
		FirebaseConnect.InitializeFirebase();
	}

	public override void _Ready()
	{
		if (InitialDisplay == null || string.IsNullOrEmpty(InitialDisplay.Name))
		{
			GD.Print("Initial display name is empty, can't instance it");
			return;
		}
		if (Displays == null || !Displays.TryGet(InitialDisplay.Name, out PackedScene firstPanel))
		{
			GD.PrintErr($"Initial display '{InitialDisplay}' is not in the display list");
			return;
		}
		UIContainerSpace.AddChild(firstPanel.Instantiate());
	}
	
	public override void _ExitTree()
	{
		Current = null;
	}

	public void SwapTo(Control current, DisplayName displayName, Action<Node> configure = null)
    {
        if (displayName == null || string.IsNullOrEmpty(displayName.Name))
        {
            GD.PrintErr("SwapTo: display name is empty");
            return;
        }

        if (Displays == null || !Displays.TryGet(displayName.Name, out PackedScene scene))
        {
            GD.PrintErr($"SwapTo: no display registered under '{displayName}'");
            return;
        }

        Node nextScene = scene.Instantiate();
        configure?.Invoke(nextScene);
        current.Visible = false;
        UIContainerSpace.AddChild(nextScene);
        current.QueueFree();
    }
}

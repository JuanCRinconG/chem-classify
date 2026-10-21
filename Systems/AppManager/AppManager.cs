using Godot;
using System;

public partial class AppManager : Node
{

	[Export]
	public Control UIContainerSpace;

	[Export(PropertyHint.None, DisplayList.GetDisplay)]
	public StringName InitialDisplay;

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
		if (string.IsNullOrEmpty(InitialDisplay))
		{
			GD.Print("Initial display name is empty, can't instance it");
			return;
		}
		if (Displays == null || !Displays.TryGet(InitialDisplay, out PackedScene firstPanel))
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

	public void SwapTo(Control current, StringName displayName, Action<Node> configure = null)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            GD.PrintErr("SwapTo: display name is empty");
            return;
        }

        if (Displays == null || !Displays.TryGet(displayName, out PackedScene scene))
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

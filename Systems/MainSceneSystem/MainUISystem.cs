using Godot;
using System;

public partial class MainUISystem : Control
{
	[Export]
	public Control UIContainerSpace;

	[Export]
	public PackedScene FirstUIPanel;

	public static MainUISystem CurrentInstance;
	
	public override void _EnterTree()
	{
		CurrentInstance = this;
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
		CurrentInstance = null;
	}
}

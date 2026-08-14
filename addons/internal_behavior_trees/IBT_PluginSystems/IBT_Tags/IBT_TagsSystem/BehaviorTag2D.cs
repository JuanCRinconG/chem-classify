#nullable enable
using Godot;

namespace IBTPlugin;


[GlobalClass]
public partial class BehaviorTag2D : Marker2D, BehaviorTag.Tag
{
	[Export]
	public Node TargetEntity { get; set; } = null!;

	[Export]
	public bool BehaviorTagEnabled { get; set; } = true;

	public override void _EnterTree()
	{
		BehaviorTag.TryRegister(TargetEntity);
	}

	public override void _ExitTree()
	{
		if (TargetEntity != null && GodotObject.IsInstanceValid(TargetEntity))
		{
			BehaviorTag.TryUnregister(TargetEntity);
		}
	}

	public override void _Ready()
	{
		if (!BehaviorTagEnabled)
		{
			return;
		}

		BehaviorTag.SpawnVisual(this, IBTVariables.BehaviorTag2DScene);
	}
}

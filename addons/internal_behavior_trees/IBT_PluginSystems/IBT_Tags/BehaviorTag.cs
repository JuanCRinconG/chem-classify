using Godot;

namespace IBTPlugin;

[GlobalClass]
public partial class BehaviorTag : Node, Tag
{
	[Export]
	public Node TargetEntity {get; set;}

	public override void _Ready()
	{
		if (!IBTSettings.GetIncludeRuntimeEditor())
		{
			return;
		}

		if (TargetEntity == null || !GodotObject.IsInstanceValid(TargetEntity))
		{
			GD.PushError($"{TargetEntity.GetPath()}: {TargetEntity.Name} is not valid.");
			return;
		}

		if (TargetEntity is not EditableBehaviorChart)
		{
			GD.PushError(
				$"{TargetEntity.GetPath()}: {TargetEntity.Name} must implement {nameof(EditableBehaviorChart)}.");
			return;
		}

		if (!TargetEntity.IsInGroup(IBTVariables.BehaviorTagGroup))
		{
			TargetEntity.AddToGroup(IBTVariables.BehaviorTagGroup);
		}
	}

	public override void _ExitTree()
	{
		if (!IBTSettings.GetIncludeRuntimeEditor())
		{
			return;
		}

		if (TargetEntity.IsInsideTree() && TargetEntity.IsInGroup(IBTVariables.BehaviorTagGroup))
		{
			TargetEntity.RemoveFromGroup(IBTVariables.BehaviorTagGroup);
		}
	}
}

public interface Tag
{
	public Node TargetEntity { get; set; }
}
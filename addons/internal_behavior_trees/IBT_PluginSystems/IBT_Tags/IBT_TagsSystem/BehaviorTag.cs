using Godot;
using IBTSystem;

namespace IBTPlugin;

public static class BehaviorTag
{
	public interface Tag
	{
		public Node TargetEntity { get; set; }
	}

	public static bool TryRegister(Node host)
	{
		if (!IBTSettings.GetIncludeRuntimeEditor())
		{
			return false;
		}

		if (host == null || !GodotObject.IsInstanceValid(host))
		{
			GD.PushError($"{host.GetPath()}: {host.Name} is not valid.");
			return false;
		}

		if (host is not EditableBehaviorChart)
		{
			GD.PushError(
				$"{host.GetPath()}: {host.Name} must implement {nameof(EditableBehaviorChart)}.");
			return false;
		}

		if (!host.IsInGroup(IBTVariables.BehaviorTagGroup))
		{
			host.AddToGroup(IBTVariables.BehaviorTagGroup);
		}

		return true;
	}

	public static void TryUnregister(Node host)
	{
		if (!IBTSettings.GetIncludeRuntimeEditor())
		{
			return;
		}

		if (host.IsInsideTree() && host.IsInGroup(IBTVariables.BehaviorTagGroup))
		{
			host.RemoveFromGroup(IBTVariables.BehaviorTagGroup);
		}
	}

	public static void SpawnVisual(Node marker, PackedScene visualScene)
	{
		if (marker.GetNodeOrNull("BehaviorTagVisual") != null)
		{
			return;
		}

		Node visual = visualScene.Instantiate();
		visual.Name = "BehaviorTagVisual";
		marker.AddChild(visual);
	}
}
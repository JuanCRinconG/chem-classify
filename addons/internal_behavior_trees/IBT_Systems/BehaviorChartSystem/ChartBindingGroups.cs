#nullable enable
using Godot;

namespace IBTSystem;

/// <summary>
/// Normalizes chart binding-group names shared by board and emitter nodes.
/// Unset, default, and empty <see cref="StringName"/> values resolve to
/// <see cref="BehaviorEngine.DefaultGroupName"/>.
/// </summary>
public static class ChartBindingGroups
{
	/// <summary>
	/// Returns <paramref name="group"/> when set; otherwise
	/// <see cref="BehaviorEngine.DefaultGroupName"/>.
	/// Safe for Godot default/uninitialized <see cref="StringName"/> (e.g. tuple
	/// <c>(target, default)</c> in <see cref="BehaviorEngine.SetBoards"/>).
	/// </summary>
	public static StringName Normalize(StringName group) =>
		string.IsNullOrEmpty(group) ? BehaviorEngine.DefaultGroupName : group;

	public static StringName Normalize(string? group)
	{
		if (string.IsNullOrWhiteSpace(group))
		{
			return BehaviorEngine.DefaultGroupName;
		}

		return new StringName(group.Trim());
	}

	public static string Display(StringName group) =>
		string.IsNullOrEmpty(group) || group == BehaviorEngine.DefaultGroupName
			? BehaviorEngine.DefaultGroupName
			: group.ToString();

	public static bool GroupsEqual(StringName a, StringName b) =>
		Normalize(a) == Normalize(b);

	/// <summary>Suggests the next unused binding group for an emitter type on this chart.</summary>
	public static StringName SuggestEmitterGroup(IBTChart chart, string emitterTypeName)
	{
		emitterTypeName = BehaviorDataKeys.Normalize(emitterTypeName);
		if (string.IsNullOrEmpty(emitterTypeName))
		{
			return BehaviorEngine.DefaultGroupName;
		}

		if (!chart.HasTransitionEngineBinding(emitterTypeName, BehaviorEngine.DefaultGroupName))
		{
			return BehaviorEngine.DefaultGroupName;
		}

		for (int index = 2; ; index++)
		{
			var candidate = new StringName($"{BehaviorEngine.DefaultGroupName}_{index}");
			if (!chart.HasTransitionEngineBinding(emitterTypeName, candidate))
			{
				return candidate;
			}
		}
	}
}

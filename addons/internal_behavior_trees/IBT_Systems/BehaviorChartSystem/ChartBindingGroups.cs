#nullable enable

namespace IBTSystem;

/// <summary>
/// Normalizes chart binding-group names shared by board and emitter nodes.
/// Stored chart values use null for the default group; runtime dictionaries use
/// <see cref="Canonical"/> (empty string) as the default key.
/// </summary>
public static class ChartBindingGroups
{
	public static bool IsDefault(string? group) =>
		string.IsNullOrWhiteSpace(group);

	/// <summary>Persisted chart value. Null means default group.</summary>
	public static string? ToStored(string? group) =>
		IsDefault(group) ? null : group!.Trim();

	/// <summary>Runtime dictionary key. Empty string means default group.</summary>
	public static string Canonical(string? group) =>
		IsDefault(group) ? string.Empty : group!.Trim();

	public static bool GroupsEqual(string? a, string? b) =>
		Canonical(a) == Canonical(b);

	/// <summary>Debug and warning text for the default group.</summary>
	public static string FormatForLog(string? group) =>
		IsDefault(group) ? "(default)" : group!.Trim();

	/// <summary>
	/// Suggests the next unused binding group for a duplicate emitter type on this chart.
	/// Returns null when the default slot is still free.
	/// </summary>
	public static string? SuggestEmitterGroup(IBTChart chart, string emitterTypeName, string groupPrefix)
	{
		emitterTypeName = BehaviorDataKeys.Normalize(emitterTypeName);
		if (string.IsNullOrEmpty(emitterTypeName))
		{
			return null;
		}

		if (!chart.HasTransitionEngineBinding(emitterTypeName, null))
		{
			return null;
		}

		for (int index = 2; ; index++)
		{
			string candidate = $"{groupPrefix}_{index}";
			if (!chart.HasTransitionEngineBinding(emitterTypeName, candidate))
			{
				return candidate;
			}
		}
	}
}

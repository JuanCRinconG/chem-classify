#if TOOLS
#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using IBTSystem;

namespace IBTPlugin;

public static class IBTEditorHelpers
{
	public static void PopulateFromData(
		OptionButton selector,
		Func<IBTData, IReadOnlyList<string>> getNames,
		string requestedSelection,
		string emptyLabel)
	{
		if (!IBTChartCreatorUI.TryGetHealthyData(out IBTData data))
		{
			PopulateKeyedSelector(selector, Array.Empty<string>(), requestedSelection, emptyLabel);
			return;
		}

		PopulateKeyedSelector(selector, getNames(data), requestedSelection, emptyLabel);
	}

	public static void PopulateBehaviorGroupSelector(OptionButton groupSelector) =>
		PopulateScanGroupSelector(groupSelector, data => data.Groups);

	public static void PopulateEmitterGroupSelector(OptionButton groupSelector) =>
		PopulateScanGroupSelector(groupSelector, data => data.EmitterGroups);

	public static void PopulateBehaviorSelectorForGroup(
		OptionButton groupSelector,
		OptionButton behaviorSelector) =>
		PopulateItemSelectorForGroup(
			groupSelector,
			behaviorSelector,
			(data, groupName) => data.GetBehaviorsFromGroup(groupName),
			IBTVariables.NoBehaviorsLabel,
			BehaviorDataKeys.GetBehaviorShortName);

	public static void PopulateEmitterSelectorForGroup(
		OptionButton groupSelector,
		OptionButton emitterSelector) =>
		PopulateItemSelectorForGroup(
			groupSelector,
			emitterSelector,
			(data, groupName) => data.GetEmittersFromGroup(groupName),
			IBTVariables.NoEmittersLabel);

	private static void PopulateScanGroupSelector(
		OptionButton groupSelector,
		Func<IBTData, Godot.Collections.Dictionary<string, Godot.Collections.Array<string>>> getGroups)
	{
		PopulateFromData(
			groupSelector,
			data =>
			{
				var names = new List<string>(getGroups(data).Keys);
				names.Sort(StringComparer.Ordinal);
				return names;
			},
			groupSelector.SelectedMetadataOrEmpty(),
			IBTVariables.NoGroupsLabel);
	}

	private static void PopulateItemSelectorForGroup(
		OptionButton groupSelector,
		OptionButton itemSelector,
		Func<IBTData, string, string[]> getItemsFromGroup,
		string emptyLabel,
		Func<string, string>? getDisplayName = null)
	{
		string groupName = groupSelector.SelectedMetadataOrEmpty();
		if (!IBTChartCreatorUI.TryGetHealthyData(out IBTData data)
			|| string.IsNullOrEmpty(groupName))
		{
			PopulateKeyedSelector(
				itemSelector,
				Array.Empty<string>(),
				itemSelector.SelectedMetadataOrEmpty(),
				emptyLabel);
			return;
		}

		PopulateKeyedSelector(
			itemSelector,
			getItemsFromGroup(data, groupName),
			itemSelector.SelectedMetadataOrEmpty(),
			emptyLabel,
			getDisplayName);
	}

	private static void PopulateKeyedSelector(
		OptionButton selector,
		IReadOnlyList<string> keys,
		string requestedSelection,
		string emptyLabel,
		Func<string, string>? getDisplayName = null)
	{
		selector.Clear();
		if (keys.Count == 0)
		{
			selector.AddItem(emptyLabel);
			selector.SetItemDisabled(0, true);
			selector.Disabled = true;
			return;
		}

		selector.Disabled = false;
		int selectedIndex = 0;
		for (int i = 0; i < keys.Count; i++)
		{
			string key = keys[i];
			selector.AddItem(getDisplayName?.Invoke(key) ?? key);
			selector.SetItemMetadata(i, key);
			if (string.Equals(key, requestedSelection, StringComparison.Ordinal))
			{
				selectedIndex = i;
			}
		}

		selector.Select(selectedIndex);
	}

	/// <summary>
	/// Sets disabled state and swaps tooltip: authored (enabled) vs blocked reason (disabled).
	/// Scene-authored enabled tooltips are cached lazily on first apply.
	/// </summary>
	public static void ApplyControlAvailability(
		Control control,
		IBTVariables.ControlAvailability availability)
	{
		if (control == null)
		{
			return;
		}

		if (!control.HasMeta(IBTVariables.AuthoredTooltipMeta))
		{
			control.SetMeta(IBTVariables.AuthoredTooltipMeta, control.TooltipText ?? string.Empty);
		}

		string authoredTooltip = control.GetMeta(IBTVariables.AuthoredTooltipMeta).AsString();
		if (control is BaseButton button)
		{
			button.Disabled = !availability.Enabled;
		}

		control.TooltipText = availability.Enabled
			? authoredTooltip
			: (!string.IsNullOrEmpty(availability.BlockedReason) ? availability.BlockedReason : authoredTooltip);
	}
}

public static class OptionButtonExtensions
{
	public static string SelectedMetadataOrEmpty(this OptionButton selector)
	{
		if (selector == null || selector.Disabled || selector.Selected < 0)
		{
			return string.Empty;
		}

		return selector.GetItemMetadata(selector.Selected).AsString();
	}

	public static bool TryGetSelectedMetadata(this OptionButton selector, out string key)
	{
		key = selector.SelectedMetadataOrEmpty();
		return !string.IsNullOrEmpty(key);
	}
}
#endif

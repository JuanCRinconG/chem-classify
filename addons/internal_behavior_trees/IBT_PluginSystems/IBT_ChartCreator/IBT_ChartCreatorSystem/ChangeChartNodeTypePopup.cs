#if TOOLS
using System;
using Godot;

namespace IBTPlugin;

public enum ChartNodeTypePickerMode
{
	Behavior,
	TransitionEngine,
}

[Tool]
public partial class ChangeChartNodeTypePopup : ConfirmationDialog
{
	[Export]
	public OptionButton GroupSelector;

	[Export]
	public OptionButton ItemSelector;

	private readonly IBTBindings _uiBindings = new();
	private Action<string> _onConfirmed;
	private ChartNodeTypePickerMode _mode;
	private string _requestedKey = string.Empty;

	public static void Open(
		PackedScene popupScene,
		ChartNodeTypePickerMode mode,
		Action<string> onConfirmed,
		SceneTree sceneTree,
		string requestedKey = "",
		string title = null)
	{
		if (popupScene?.Instantiate() is not ChangeChartNodeTypePopup popup)
		{
			return;
		}

		sceneTree.Root.AddChild(popup);
		popup._mode = mode;
		popup._requestedKey = requestedKey ?? string.Empty;
		popup.ShowFor(onConfirmed, title ?? GetDefaultTitle(mode, popup._requestedKey));
	}

	public override void _Ready()
	{
		SetProcessUnhandledInput(true);
		BindDialogControls();
	}

	public override void _ExitTree() => _uiBindings.Clear();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_accept"))
		{
			return;
		}

		Button okButton = GetOkButton();
		if (okButton == null || okButton.Disabled)
		{
			return;
		}

		ApplySelection();
		GetViewport().SetInputAsHandled();
	}

	private void BindDialogControls()
	{
		_uiBindings.Clear();
		_uiBindings.BindSignal(this, SignalName.Confirmed, new Callable(this, MethodName.ApplySelection));
		_uiBindings.BindSignal(this, SignalName.Canceled, new Callable(this, MethodName.OnDialogCanceled));
		_uiBindings.BindOptionButton(GroupSelector, OnGroupSelected);
	}

	private void ShowFor(Action<string> onConfirmed, string title)
	{
		_onConfirmed = onConfirmed;
		Title = title;
		PopulateSelectors();
		PopupCentered();
	}

	private void PopulateSelectors()
	{
		switch (_mode)
		{
			case ChartNodeTypePickerMode.Behavior:
				IBTEditorHelpers.PopulateBehaviorGroupSelector(GroupSelector);
				break;
			case ChartNodeTypePickerMode.TransitionEngine:
				IBTEditorHelpers.PopulateEmitterGroupSelector(GroupSelector);
				break;
		}

		if (!string.IsNullOrEmpty(_requestedKey)
			&& IBTChartCreatorUI.TryGetHealthyData(out IBTData data)
			&& data.TryFindGroupForOwner(_requestedKey, out string groupName))
		{
			SelectGroupByMetadata(groupName);
		}

		PopulateItemSelector();

		if (!string.IsNullOrEmpty(_requestedKey))
		{
			SelectItemByMetadata(ItemSelector, _requestedKey);
		}
	}

	private void OnDialogCanceled() => QueueFree();

	private void ApplySelection()
	{
		if (!ItemSelector.TryGetSelectedMetadata(out string key))
		{
			return;
		}

		_onConfirmed?.Invoke(key);
		QueueFree();
	}

	private static string GetDefaultTitle(ChartNodeTypePickerMode mode, string requestedKey) =>
		mode switch
		{
			ChartNodeTypePickerMode.Behavior => "Change Behavior Type",
			ChartNodeTypePickerMode.TransitionEngine =>
				string.IsNullOrEmpty(requestedKey)
					? "Add Transition Engine"
					: "Change Transition Engine Type",
			_ => "Change Type",
		};

	private void OnGroupSelected(long _) => PopulateItemSelector();

	private void PopulateItemSelector()
	{
		switch (_mode)
		{
			case ChartNodeTypePickerMode.Behavior:
				IBTEditorHelpers.PopulateBehaviorSelectorForGroup(GroupSelector, ItemSelector);
				break;
			case ChartNodeTypePickerMode.TransitionEngine:
				IBTEditorHelpers.PopulateEmitterSelectorForGroup(GroupSelector, ItemSelector);
				break;
		}

		GetOkButton().Disabled = ItemSelector.Disabled;
	}

	private void SelectGroupByMetadata(string groupName)
	{
		for (int i = 0; i < GroupSelector.ItemCount; i++)
		{
			if (string.Equals(
				GroupSelector.GetItemMetadata(i).AsString(),
				groupName,
				StringComparison.Ordinal))
			{
				GroupSelector.Select(i);
				return;
			}
		}
	}

	private static void SelectItemByMetadata(OptionButton selector, string key)
	{
		for (int i = 0; i < selector.ItemCount; i++)
		{
			if (string.Equals(selector.GetItemMetadata(i).AsString(), key, StringComparison.Ordinal))
			{
				selector.Select(i);
				return;
			}
		}
	}

}
#endif

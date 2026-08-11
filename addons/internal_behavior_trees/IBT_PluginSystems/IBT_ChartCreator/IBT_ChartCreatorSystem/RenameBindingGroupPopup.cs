#if TOOLS
#nullable enable
using System;
using Godot;

namespace IBTPlugin;

[Tool]
public partial class RenameBindingGroupPopup : ConfirmationDialog
{
	[Export]
	public Label CurrentGroupLabel = null!;

	[Export]
	public LineEdit NewGroupLineEdit = null!;

	private readonly IBTBindings _bindings = new();
	private Action<StringName>? _onConfirmed;

	public static void Open(
		PackedScene popupScene,
		SceneTree sceneTree,
		StringName currentGroup,
		Action<StringName> onConfirmed)
	{
		if (popupScene.Instantiate() is not RenameBindingGroupPopup popup)
		{
			return;
		}

		sceneTree.Root.AddChild(popup);
		popup.ShowFor(currentGroup, onConfirmed);
	}

	public override void _Ready()
	{
		_bindings.BindSignal(this, SignalName.Confirmed, Callable.From(ApplySelection));
		_bindings.BindSignal(this, SignalName.Canceled, Callable.From(OnCanceled));
		_bindings.Bind<LineEdit.TextSubmittedEventHandler>(
			add => NewGroupLineEdit.TextSubmitted += add,
			remove => NewGroupLineEdit.TextSubmitted -= remove,
			_ => ApplySelection());
	}

	public override void _ExitTree() => _bindings.Clear();

	private void ShowFor(StringName currentGroup, Action<StringName> onConfirmed)
	{
		_onConfirmed = onConfirmed;
		string displayGroup = ChartBindingGroups.Display(currentGroup);
		CurrentGroupLabel.Text = displayGroup;
		NewGroupLineEdit.Text = displayGroup;
		NewGroupLineEdit.SelectAll();
		PopupCentered();
		NewGroupLineEdit.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void OnCanceled() => QueueFree();

	private void ApplySelection()
	{
		_onConfirmed?.Invoke(ChartBindingGroups.Normalize(NewGroupLineEdit.Text));
		QueueFree();
	}
}
#endif

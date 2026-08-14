#if TOOLS
#nullable enable
using System;
using Godot;
using IBTSystem;

namespace IBTPlugin;

[Tool]
public partial class RenameBindingGroupPopup : ConfirmationDialog
{
	[Export]
	public Label CurrentGroupLabel = null!;

	[Export]
	public LineEdit NewGroupLineEdit = null!;

	private readonly IBTBindings _bindings = new();
	private Action<string?>? _onConfirmed;

	public static void Open(
		PackedScene popupScene,
		SceneTree sceneTree,
		string? currentGroup,
		Action<string?> onConfirmed)
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
		_bindings.BindLineEditCommit(NewGroupLineEdit, ApplySelection);
	}

	public override void _ExitTree() => _bindings.Clear();

	private void ShowFor(string? currentGroup, Action<string?> onConfirmed)
	{
		_onConfirmed = onConfirmed;
		string text = currentGroup ?? string.Empty;
		CurrentGroupLabel.Text = text;
		NewGroupLineEdit.Text = text;
		NewGroupLineEdit.SelectAll();
		PopupCentered();
		NewGroupLineEdit.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void OnCanceled() => QueueFree();

	private void ApplySelection()
	{
		_onConfirmed?.Invoke(ChartBindingGroups.ToStored(NewGroupLineEdit.Text));
		QueueFree();
	}
}
#endif

#nullable enable
using Godot;
using IBTSystem;

namespace IBTPlugin;

/// <summary>
/// Runtime behavior wire editor autoload panel: scan gate, failure space, and entity discovery.
/// </summary>
public partial class IBTRuntimeEditorUI : PanelContainer
{
	[Export]
	public Control FailureSpace { get; set; } = null!;

	[Export]
	public Label FailureTitle { get; set; } = null!;

	[Export]
	public Label FailureHint { get; set; } = null!;

	[Export]
	public ItemList IssuesList { get; set; } = null!;

	[Export]
	public Control AuthoringWorkspace { get; set; } = null!;

	[Export]
	public OptionButton EntitySelector { get; set; } = null!;

	[Export]
	public IBTRuntimeGraphEdit GraphView { get; set; } = null!;

	[Export]
	public Label KeyDisplayLabel { get; set; } = null!;

	private Key _visibilityToggleKey;

	public override void _Ready()
	{
		_visibilityToggleKey = IBTSettings.GetRuntimeEditorToggleKey();
		KeyDisplayLabel.Text = _visibilityToggleKey.ToString();

		ApplyPanelFromState();
		ApplyMouseFilterFromVisibility();
		CallDeferred(MethodName.RefreshDiscoveredEntities);
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationVisibilityChanged)
		{
			ApplyMouseFilterFromVisibility();
		}
	}

	public void RefreshDiscoveredEntities()
	{
		EntitySelector.Clear();
		if (!AuthoringWorkspace.Visible)
		{
			return;
		}

		Godot.Collections.Array<Node> hosts = GetTree().GetNodesInGroup(IBTVariables.BehaviorTagGroup);
		int itemIndex = 0;
		for (int i = 0; i < hosts.Count; i++)
		{
			Node hostNode = hosts[i];
			if (hostNode is not EditableBehaviorChart || !hostNode.IsValid())
			{
				continue;
			}

			EntitySelector.AddItem(hostNode.Name);
			EntitySelector.SetItemMetadata(itemIndex, hostNode);
			itemIndex++;
		}

		if (itemIndex > 0)
		{
			EntitySelector.Select(0);
		}

		GraphView?.OnDiscoveredEntitiesReady();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey key
			|| key.PhysicalKeycode != _visibilityToggleKey
			|| !key.IsPressed()
			|| key.Echo)
		{
			return;
		}

		Visible = !Visible;
	}

	private void ApplyPanelFromState()
	{
		BehaviorScanGatePresenter.Apply(
			FailureSpace,
			FailureTitle,
			FailureHint,
			IssuesList,
			AuthoringWorkspace,
			IBTManager.GetRuntimeData(),
			new ScanGateLabels
			{
				NoDataTitle = IBTVariables.RuntimeFailureTitleNoData,
				NoDataHint = IBTVariables.RuntimeFailureHintNoData,
				DirtyTitle = IBTVariables.RuntimeFailureTitleDirty,
				DirtyHint = IBTVariables.RuntimeFailureHintDirty,
			});
	}

	private void ApplyMouseFilterFromVisibility()
	{
		MouseFilter = Visible && IsVisibleInTree() ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
	}
}

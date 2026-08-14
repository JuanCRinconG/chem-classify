#nullable enable
using Godot;
using IBTSystem;

namespace IBTPlugin;

public partial class IBTTag : PanelContainer
{
	private const string NoChildrenLabel = "(no children)";

	[Export]
	public Label BehaviorLabel { get; set; } = null!;

	[Export]
	public Label ChildProcessModeLabel { get; set; } = null!;

	[Export]
	public Tree ChildTree { get; set; } = null!;

	private BehaviorEngine? _engine;

	public override void _Ready()
	{
		FocusMode = FocusModeEnum.None;
		MouseFilter = MouseFilterEnum.Stop;
		TryBindFromMarker();
	}

	public override void _ExitTree()
	{
		UnsubscribeEngine();
	}

	private void TryBindFromMarker()
	{
		BehaviorTag.Tag? marker = FindMarkerAncestor();
		if (marker == null)
		{
			GD.PushError($"{GetPath()}: no {nameof(BehaviorTag.Tag)} ancestor found.");
			return;
		}

		Node targetEntity = marker.TargetEntity;
		if (targetEntity is not EditableBehaviorChart host)
		{
			GD.PushError(
				$"{GetPath()}: marker {nameof(BehaviorTag.Tag.TargetEntity)} must implement {nameof(EditableBehaviorChart)}.");
			return;
		}

		host.GetRuntimeInfo(out IBTChart chart, out BehaviorEngine engine);
		if (chart == null || !GodotObject.IsInstanceValid(chart))
		{
			GD.PushError($"{GetPath()}: host returned an invalid behavior chart.");
			return;
		}

		SubscribeEngine(engine);
		RefreshFromActiveInstance(engine.ActiveBehaviorInstanceId);
	}

	private BehaviorTag.Tag? FindMarkerAncestor()
	{
		for (Node? node = GetParent(); node != null; node = node.GetParent())
		{
			if (node is BehaviorTag.Tag marker)
			{
				return marker;
			}
		}

		return null;
	}

	private void SubscribeEngine(BehaviorEngine? engine)
	{
		UnsubscribeEngine();
		if (engine == null)
		{
			return;
		}

		_engine = engine;
		_engine.ActiveBehaviorInstanceChanged += OnActiveBehaviorInstanceChanged;
	}

	private void UnsubscribeEngine()
	{
		if (_engine == null)
		{
			return;
		}

		_engine.ActiveBehaviorInstanceChanged -= OnActiveBehaviorInstanceChanged;
		_engine = null;
	}

	private void OnActiveBehaviorInstanceChanged(string instanceId) =>
		RefreshFromActiveInstance(instanceId);

	private void RefreshFromActiveInstance(string instanceId)
	{
		BehaviorCore? activeCore = _engine?.ActiveBehavior;
		if (activeCore == null || string.IsNullOrEmpty(instanceId))
		{
			ApplyEmptyState();
			return;
		}

		BehaviorLabel.Text = FormatCoreLabel(activeCore);
		ChildProcessModeLabel.Text = FormatProcessOrder(activeCore.CurrentProcessOrder);
		RebuildTree(activeCore);
	}

	private void RebuildTree(BehaviorCore? activeCore)
	{
		ChildTree.Clear();

		if (activeCore == null || activeCore.Children.Count == 0)
		{
			SetSingleTreeItem(NoChildrenLabel);
			return;
		}

		for (int i = 0; i < activeCore.Children.Count; i++)
		{
			BehaviorCore child = activeCore.Children[i];
			TreeItem? item = ChildTree.CreateItem();
			if (item == null)
			{
				continue;
			}

			item.SetText(0, FormatCoreLabel(child));
			item.SetSelectable(0, false);
			PopulateTreeChildren(item, child);
		}
	}

	private void SetSingleTreeItem(string label)
	{
		TreeItem? item = ChildTree.CreateItem();
		if (item == null)
		{
			return;
		}

		item.SetText(0, label);
		item.SetSelectable(0, false);
	}

	private static void PopulateTreeChildren(TreeItem parent, BehaviorCore core)
	{
		Tree tree = parent.GetTree();
		for (int i = 0; i < core.Children.Count; i++)
		{
			BehaviorCore child = core.Children[i];
			TreeItem item = tree.CreateItem(parent);
			item.SetText(0, FormatCoreLabel(child));
			item.SetSelectable(0, false);
			PopulateTreeChildren(item, child);
		}
	}

	private static string FormatCoreLabel(BehaviorCore core) => core.GetType().Name;

	private void ApplyEmptyState()
	{
		BehaviorLabel.Text = "(no behavior)";
		ChildProcessModeLabel.Text = "No child process";
		RebuildTree(null);
	}

	private static string FormatProcessOrder(BehaviorProcessOrder order) =>
		order switch
		{
			BehaviorProcessOrder.NoChildProcess => "No child process",
			BehaviorProcessOrder.ParentBeforeChild => "Parent before child",
			BehaviorProcessOrder.ParentAfterChild => "Parent after child",
			_ => order.ToString(),
		};
}

#if TOOLS
#nullable enable
using Godot;

namespace IBTPlugin;

[Tool]
public partial class BoardsWorkspace : Control, IBTNodeComponent
{
	[Export]
	public Label NoBoardsLabel = null!;

	[Export]
	public Control BoardsWorkspaceNode = null!;

	[Export]
	public Label BoardNameLabel = null!;

	[Export]
	public LineEdit BoardGroupLineEdit = null!;

	[Export]
	public Button RenameBoardGroupButton = null!;

	private readonly IBTBindings _bindings = new();
	private IBTBehaviorNode _graphNode = null!;
	private bool _suppressBoardGroupChanged;

	public void BindGraphNode(IBTTransitionNode graphNode) =>
		_graphNode = (IBTBehaviorNode)graphNode;

	public override void _ExitTree()
	{
		base._ExitTree();
		_bindings.Clear();
	}

	public override void _Ready()
	{
		_bindings.Bind<LineEdit.TextSubmittedEventHandler>(
			add => BoardGroupLineEdit.TextSubmitted += add,
			remove => BoardGroupLineEdit.TextSubmitted -= remove,
			_ => CommitBoardGroup());
		_bindings.BindSignal(
			BoardGroupLineEdit,
			Control.SignalName.FocusExited,
			Callable.From(CommitBoardGroup));
		_bindings.BindButton(RenameBoardGroupButton, OpenRenameBoardGroupPopup);
	}

	public void RefreshFromChart(IBTData? data)
	{
		if (_graphNode.TransitionNode is not BehaviorNode chartNode)
		{
			NoBoardsLabel.Visible = true;
			BoardsWorkspaceNode.Visible = false;
			return;
		}

		string boardTypeName = data?.GetBehaviorBoardTypeName(chartNode.FullTypeName) ?? string.Empty;
		bool hasBoard = !string.IsNullOrWhiteSpace(boardTypeName);
		NoBoardsLabel.Visible = !hasBoard;
		BoardsWorkspaceNode.Visible = hasBoard;

		if (!hasBoard)
		{
			return;
		}

		BoardNameLabel.Text = boardTypeName;
		_suppressBoardGroupChanged = true;
		BoardGroupLineEdit.Text = ChartBindingGroups.Display(chartNode.BoardGroup);
		_suppressBoardGroupChanged = false;
	}

	private void CommitBoardGroup()
	{
		if (_suppressBoardGroupChanged || _graphNode.TransitionNode is not BehaviorNode chartNode)
		{
			return;
		}

		StringName boardGroup = ChartBindingGroups.Normalize(BoardGroupLineEdit.Text);
		if (ChartBindingGroups.GroupsEqual(chartNode.BoardGroup, boardGroup))
		{
			return;
		}

		chartNode.BoardGroup = boardGroup;
		BehaviorGraphEditor.CurrentInstance?.CommitNodeMutation();
	}

	private void OpenRenameBoardGroupPopup()
	{
		if (_graphNode.TransitionNode is not BehaviorNode chartNode)
		{
			return;
		}

		BehaviorGraphEditor? editor = BehaviorGraphEditor.CurrentInstance;
		if (editor?.RenameBindingGroupPopupScene == null)
		{
			return;
		}

		StringName currentGroup = chartNode.BoardGroup;
		RenameBindingGroupPopup.Open(
			editor.RenameBindingGroupPopupScene,
			GetTree(),
			currentGroup,
			newGroup => editor.RequestRenameBoardGroup(currentGroup, newGroup));
	}
}
#endif

#if TOOLS
#nullable enable
using Godot;
using IBTSystem;

namespace IBTPlugin;

[Tool]
public partial class GroupEditorWorkspace : Control, IBTNodeComponent
{
	[Export]
	public LineEdit GroupNameLineEdit = null!;

	private readonly IBTBindings _bindings = new();
	private IBTTransitionNode _graphNode = null!;
	private bool _suppressGroupEdit;

	public void BindGraphNode(IBTTransitionNode graphNode) => _graphNode = graphNode;

	public override void _ExitTree()
	{
		base._ExitTree();
		_bindings.Clear();
	}

	public override void _Ready()
	{
		_bindings.BindLineEditCommit(GroupNameLineEdit, ApplyGroupName);
	}

	public void RefreshFromChart(IBTData? data)
	{
		if (_graphNode.TransitionNode is not TransitionEngineNode chartNode
			|| _graphNode is IBTBehaviorNode)
		{
			Visible = false;
			return;
		}

		Visible = true;
		_suppressGroupEdit = true;
		GroupNameLineEdit.Text = chartNode.BindingGroup ?? string.Empty;
		_suppressGroupEdit = false;
	}

	private void ApplyGroupName()
	{
		if (_suppressGroupEdit
			|| _graphNode.TransitionNode is not TransitionEngineNode chartNode
			|| BehaviorGraphEditor.CurrentInstance?.ActiveChart is not IBTChart chart)
		{
			return;
		}

		string? bindingGroup = ChartBindingGroups.ToStored(GroupNameLineEdit.Text);
		if (ChartBindingGroups.GroupsEqual(chartNode.BindingGroup, bindingGroup))
		{
			return;
		}

		if (!chart.TrySetTransitionEngineBindingGroup(_graphNode.InstanceId, bindingGroup))
		{
			IBTVariables.LogWarn(
				"Chart",
				$"Binding group '{ChartBindingGroups.FormatForLog(bindingGroup)}' is already used by another "
				+ $"'{chartNode.FullTypeName}' transition engine on this chart.");
			_suppressGroupEdit = true;
			GroupNameLineEdit.Text = chartNode.BindingGroup ?? string.Empty;
			_suppressGroupEdit = false;
			return;
		}

		BehaviorGraphEditor.CurrentInstance?.CommitNodeMutation();
		_graphNode.RefreshFromChart(
			_graphNode.InstanceId,
			IBTChartCreatorUI.TryGetHealthyData(out IBTData data) ? data : null);
	}
}
#endif

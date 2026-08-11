#if TOOLS
#nullable enable
using Godot;

namespace IBTPlugin;

[Tool]
public partial class CRUDWorkspace : Control, IBTNodeComponent
{
	[Export]
	public Button DeleteButton = null!;

	[Export]
	public Button ChangeTypeButton = null!;

	[Export]
	public Label DeprecatedLabel = null!;

	private readonly IBTBindings _bindings = new();
	private IBTTransitionNode _graphNode = null!;

	public void BindGraphNode(IBTTransitionNode graphNode) => _graphNode = graphNode;

	public override void _ExitTree()
	{
		base._ExitTree();
		_bindings.Clear();
	}

	public override void _Ready()
	{
		_bindings.BindButton(DeleteButton, OnDeletePressed);
		_bindings.BindButton(ChangeTypeButton, OnChangeTypePressed);
	}

	public void RefreshFromChart(IBTData? data)
	{
		if (_graphNode is IBTBehaviorNode behaviorNode
			&& behaviorNode.TransitionNode is BehaviorNode chartNode)
		{
			DeprecatedLabel.Visible = data?.HasBehavior(chartNode.FullTypeName) != true;
			return;
		}

		string emitterTypeName =
			(_graphNode.TransitionNode as TransitionEngineNode)?.FullTypeName ?? string.Empty;
		DeprecatedLabel.Visible = data?.HasEmitter(emitterTypeName) != true;
	}

	private void OpenChangeTypePopup()
	{
		BehaviorGraphEditor? editor = BehaviorGraphEditor.CurrentInstance;
		if (editor?.ChangeChartNodeTypePopupScene == null)
		{
			return;
		}

		if (_graphNode is IBTBehaviorNode behaviorNode)
		{
			ChangeChartNodeTypePopup.Open(
				editor.ChangeChartNodeTypePopupScene,
				ChartNodeTypePickerMode.Behavior,
				behaviorName => editor.RequestChangeBehaviorType(behaviorNode.InstanceId, behaviorName),
				GetTree(),
				behaviorNode.ChartNode?.FullTypeName ?? string.Empty);
			return;
		}

		string emitterTypeName =
			(_graphNode.TransitionNode as TransitionEngineNode)?.FullTypeName ?? string.Empty;
		ChangeChartNodeTypePopup.Open(
			editor.ChangeChartNodeTypePopupScene,
			ChartNodeTypePickerMode.TransitionEngine,
			newEmitterTypeName => editor.RequestChangeTransitionEngineType(
				_graphNode.InstanceId,
				newEmitterTypeName),
			GetTree(),
			emitterTypeName);
	}

	private void OnDeletePressed()
	{
		BehaviorGraphEditor? editor = BehaviorGraphEditor.CurrentInstance;
		if (editor == null)
		{
			return;
		}

		if (_graphNode is IBTBehaviorNode behaviorNode)
		{
			editor.RequestDeleteBehavior(behaviorNode.InstanceId);
			return;
		}

		editor.RequestDeleteTransitionEngine(_graphNode.InstanceId);
	}

	private void OnChangeTypePressed() => OpenChangeTypePopup();
}
#endif

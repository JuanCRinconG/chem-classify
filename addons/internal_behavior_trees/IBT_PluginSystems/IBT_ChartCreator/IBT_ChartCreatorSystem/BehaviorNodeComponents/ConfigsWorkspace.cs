#if TOOLS
#nullable enable
using Godot;
using IBTSystem;

namespace IBTPlugin;

[Tool]
public partial class ConfigsWorkspace : Control, IBTNodeComponent
{
	[Export]
	public Label NoConfigsLabel = null!;

	[Export]
	public Control ConfigsWorkspaceNode = null!;

	[Export]
	public EditorResourcePicker ConfigPicker = null!;

	[Export]
	public Control InspectorPanel = null!;

	[Export]
	public EditorInspector ConfigInspector = null!;

	private readonly IBTBindings _bindings = new();
	private IBTBehaviorNode _graphNode = null!;
	private bool _configInspectorExpanded;

	public void BindGraphNode(IBTTransitionNode graphNode) =>
		_graphNode = (IBTBehaviorNode)graphNode;

	public override void _ExitTree()
	{
		base._ExitTree();
		_bindings.Clear();
	}

	public override void _Ready()
	{
		_bindings.Bind<EditorResourcePicker.ResourceChangedEventHandler>(
			add => ConfigPicker.ResourceChanged += add,
			remove => ConfigPicker.ResourceChanged -= remove,
			OnConfigResourceChanged);
		_bindings.Bind<EditorResourcePicker.ResourceSelectedEventHandler>(
			add => ConfigPicker.ResourceSelected += add,
			remove => ConfigPicker.ResourceSelected -= remove,
			OnConfigResourceSelected);
		_bindings.Bind<EditorInspector.PropertyEditedEventHandler>(
			add => ConfigInspector.PropertyEdited += add,
			remove => ConfigInspector.PropertyEdited -= remove,
			OnConfigPropertyEdited);
	}

	public void RefreshFromChart(IBTData? data)
	{
		if (_graphNode.TransitionNode is not BehaviorNode chartNode)
		{
			NoConfigsLabel.Visible = true;
			ConfigsWorkspaceNode.Visible = false;
			return;
		}

		string configTypeName = data?.GetBehaviorConfigTypeName(chartNode.FullTypeName) ?? string.Empty;
		bool hasConfig = !string.IsNullOrWhiteSpace(configTypeName);
		NoConfigsLabel.Visible = !hasConfig;
		ConfigsWorkspaceNode.Visible = hasConfig;

		if (!hasConfig)
		{
			ConfigPicker.BaseType = nameof(Resource);
			ConfigPicker.EditedResource = null;
			SetConfigInspectorExpanded(false);
			return;
		}

		ConfigPicker.BaseType = configTypeName;
		ConfigPicker.EditedResource = chartNode.Config;
		if (_configInspectorExpanded && chartNode.Config != null)
		{
			ConfigInspector.Edit(chartNode.Config);
			InspectorPanel.Visible = true;
		}
		else
		{
			SetConfigInspectorExpanded(false);
		}
	}

	private void SetConfigInspectorExpanded(bool expanded)
	{
		Resource? resource = ConfigPicker?.EditedResource;
		bool show = expanded && resource != null;
		_configInspectorExpanded = show;
		ConfigPicker?.SetTogglePressed(show);
		InspectorPanel.Visible = show;
		ConfigInspector.Edit(show ? resource : null);
		Callable.From(_graphNode.ResetSize).CallDeferred();
	}

	private void OnConfigResourceChanged(Resource resource)
	{
		if (_graphNode.TransitionNode is not BehaviorNode chartNode)
		{
			return;
		}

		chartNode.Config = resource;
		if (resource == null)
		{
			SetConfigInspectorExpanded(false);
		}
		else if (_configInspectorExpanded)
		{
			ConfigInspector.Edit(resource);
			_graphNode.ResetSize();
		}

		BehaviorGraphEditor.CurrentInstance?.CommitNodeMutation();
	}

	private void OnConfigResourceSelected(Resource resource, bool inspect)
	{
		if (resource == null)
		{
			SetConfigInspectorExpanded(false);
			return;
		}

		SetConfigInspectorExpanded(inspect || !_configInspectorExpanded);
	}

	private void OnConfigPropertyEdited(string _)
	{
		BehaviorGraphEditor.CurrentInstance?.CommitNodeMutation();
		_graphNode.ResetSize();
	}
}
#endif

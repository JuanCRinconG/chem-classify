#if TOOLS
using Godot;

namespace IBTPlugin;

[Tool]
public partial class IBTPlugin : EditorPlugin
{
	internal Control MainScreenRoot;

	private bool _mainScreenAttached;
	private bool _mainScreenVisible;
	private bool _exitTreeHandled;
	private bool _pluginShuttingDown;

	internal static IBTPlugin CurrentInstance { get; private set; }

	public override void _EnterTree()
	{
		_exitTreeHandled = false;
		_pluginShuttingDown = false;
		CurrentInstance = this;

		IBTSettings.Prepare();
		BuildMainScreenUi();
		AttachMainScreen();
		_MakeVisible(false);
		SetProcessInput(true);
	}

	public override void _EnablePlugin()
	{
		if (IBTSettings.GetIncludeRuntimeEditor())
		{
			AddAutoloadSingleton(IBTVariables.RuntimeEditorAutoloadName, IBTVariables.RuntimeEditorPath);
		}
	}

	public override void _DisablePlugin()
	{
		RemoveAutoloadSingleton(IBTVariables.RuntimeEditorAutoloadName);
	}

	public override void _ExitTree()
	{
		if (_exitTreeHandled)
		{
			return;
		}

		_exitTreeHandled = true;
		_pluginShuttingDown = true;
		SetProcessInput(false);
		TeardownMainScreenUi();
		CurrentInstance = null;
	}

	public override bool _HasMainScreen() => true;

	public override string _GetPluginName() => IBTVariables.PluginName;

	public override Texture2D _GetPluginIcon()
	{
		if (IBTVariables.PluginIcon != null)
		{
			return IBTVariables.PluginIcon;
		}

		return EditorInterface.Singleton.GetEditorTheme().GetIcon("Node", "EditorIcons");
	}

	public override void _MakeVisible(bool visible)
	{
		_mainScreenVisible = visible;
		if (MainScreenRoot != null && GodotObject.IsInstanceValid(MainScreenRoot))
		{
			MainScreenRoot.Visible = visible;
		}
	}

	public override bool _Handles(GodotObject @object) => @object is IBTChart;

	public override void _Edit(GodotObject @object)
	{
		if (@object is not IBTChart chart)
		{
			return;
		}

		EditorInterface.Singleton.SetMainScreenEditor(IBTVariables.PluginName);

		BehaviorGraphEditor graph = BehaviorGraphEditor.CurrentInstance;
		if (graph == null || !GodotObject.IsInstanceValid(graph))
		{
			return;
		}

		if (graph.IsNodeReady())
		{
			graph.ActiveChart = chart;
		}
		else
		{
			graph.CallDeferred(BehaviorGraphEditor.MethodName.LoadChart, chart);
		}
	}

	private void BuildMainScreenUi()
	{
		MainScreenRoot = IBTVariables.MainScreenScene.Instantiate<Control>();
		MainScreenRoot.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		MainScreenRoot.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
	}

	private void AttachMainScreen()
	{
		if (!GodotObject.IsInstanceValid(this)
			|| MainScreenRoot == null
			|| !GodotObject.IsInstanceValid(MainScreenRoot)
			|| _mainScreenAttached)
		{
			return;
		}

		EditorInterface.Singleton.GetEditorMainScreen().AddChild(MainScreenRoot);
		_mainScreenAttached = true;
	}

	private void TeardownMainScreenUi()
	{
		var panel = MainScreenRoot;
		_mainScreenAttached = false;
		_mainScreenVisible = false;
		MainScreenRoot = null;

		if (panel == null || !GodotObject.IsInstanceValid(panel))
		{
			return;
		}

		panel.QueueFree();
	}

	public override void _Input(InputEvent @event)
	{
		if (_pluginShuttingDown || !_mainScreenVisible)
		{
			return;
		}

		BehaviorGraphEditor graph = IBTChartCreatorUI.CurrentInstance?.GraphView;
		if (graph == null || !GodotObject.IsInstanceValid(graph))
		{
			return;
		}

		if (!IsDeleteKeyEvent(@event))
		{
			return;
		}

		Control focusOwner = GetViewport().GuiGetFocusOwner();
		if (!ShouldForwardDeleteKey(graph, focusOwner))
		{
			return;
		}

		if (!graph.HasSelectedGraphNodes())
		{
			return;
		}

		graph.DeleteSelectedGraphNodes();
		GetViewport().SetInputAsHandled();
	}

	private static bool IsDeleteKeyEvent(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return false;
		}

		return key.Keycode is Key.Delete or Key.Backspace
			|| key.PhysicalKeycode is Key.Delete or Key.Backspace
			|| @event.IsAction("ui_graph_delete");
	}

	private static bool ShouldForwardDeleteKey(BehaviorGraphEditor graph, Control focusOwner)
	{
		if (!graph.HasFocus() && !graph.HasSelectedGraphNodes())
		{
			return false;
		}

		if (focusOwner is LineEdit or TextEdit)
		{
			return false;
		}

		IBTChartCreatorUI dock = IBTChartCreatorUI.CurrentInstance;
		return focusOwner == null
			|| focusOwner == graph
			|| graph.IsAncestorOf(focusOwner)
			|| (dock != null && dock.IsAncestorOf(focusOwner));
	}
}
#endif

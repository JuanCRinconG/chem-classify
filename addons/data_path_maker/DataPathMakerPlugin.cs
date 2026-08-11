#if TOOLS
using Godot;

/// <summary>
/// EditorPlugin host for Data Path Maker — registers the inspector path navigator.
/// Live session root is owned by <see cref="InspectorPlugin"/> (any Resource).
/// Usability is decided by each picker's static query method.
/// </summary>
[Tool]
public partial class DataPathMakerPlugin : EditorPlugin
{
	public static DataPathMakerPlugin CurrentInstance { get; private set; }

	public DataPathMakerInspector InspectorPlugin { get; private set; }

	public override void _EnterTree()
	{
		CurrentInstance = this;
		InspectorPlugin = new DataPathMakerInspector();
		AddInspectorPlugin(InspectorPlugin);
	}

	public override void _ExitTree()
	{
		InspectorPlugin.ClearSessionRoot();
		RemoveInspectorPlugin(InspectorPlugin);
		InspectorPlugin = null;
		CurrentInstance = null;
	}
}
#endif

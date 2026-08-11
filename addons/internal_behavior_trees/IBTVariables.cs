using Godot;

namespace IBTPlugin;

/// <summary>
/// Main-screen bootstrap, blocked-reason strings, and graph constants.
/// Scene-authored chrome (including enabled tooltips) stays in the .tscn.
/// </summary>
public static class IBTVariables
{
	public const string PluginName = "Behavior";

	public static readonly StringName DefaultGroupName = BehaviorEngine.DefaultGroupName;
	
	public const string ChartCreatorPath =
		"res://addons/internal_behavior_trees/IBT_PluginSystems/IBT_ChartCreator/IBT_ChartCreatorUI/ChartCreator.tscn";

	public const string RuntimeEditorPath =
		"res://addons/internal_behavior_trees/IBT_PluginSystems/IBT_RuntimeEditor/IBT_RuntimeEditorUI/RuntimeEditor.tscn";

	public const string RuntimeEditorAutoloadName = "IBTRuntimeEditor";

	public const string PluginIconPath =
		"res://addons/internal_behavior_trees/IBT_Assets/IBTLogo.svg";

	/// <summary>Eager-loaded main-screen scene (C# equivalent of GDScript preload).</summary>
	public static readonly PackedScene MainScreenScene = GD.Load<PackedScene>(ChartCreatorPath);

	public static readonly StringName BehaviorTagGroup = "BehaviorTags";

	/// <summary>Eager-loaded plugin tab icon.</summary>
	public static readonly Texture2D PluginIcon = GD.Load<Texture2D>(PluginIconPath);

	public const string AuthoredTooltipMeta = "ibt_authored_tooltip";

	public const string EmptyDataHint =
		"No behavior data loaded. Set an IBTData.";

	public const string FailureTitleNoData = "No behavior data";
	public const string FailureHintNoData =
		"Set an IBTData, then use Refresh Data after C# rebuilds.";

	public const string FailureTitleDirty = "Attribute data scan failed";
	public const string FailureHintDirty =
		"Fix the issues below in C#, rebuild, then press Refresh Data.";

	public const string RuntimeFailureTitleNoData = "No behavior data";
	public const string RuntimeFailureHintNoData =
		"Assign an IBTData resource on the runtime editor autoload.";
	public const string RuntimeFailureTitleDirty = "Attribute data scan failed";
	public const string RuntimeFailureHintDirty =
		"Fix the scan issues below in C#, rebuild, then restart play.";

	public const string NoGroupsLabel = "(no namespaces)";
	public const string NoBehaviorsLabel = "(no behaviors)";
	public const string NoEmittersLabel = "(no transition engines)";
	public const string NoActiveChartTooltip = "Set a behavior chart before adding nodes.";
	public const string AddBehaviorNodeDisabledTooltip = "Select a namespace and behavior before adding a node.";
	public const string AddTransitionEngineDisabledTooltip =
		"No transition engine types are available in the behavior data. Add [BehaviorTransition] events to a non [Behavior] class and refresh the data.";
	public const string AddTransitionEngineNodeDisabledTooltip =
		"Select a namespace and transition engine type before adding a node.";

	/// <summary>
	/// Enabled flag plus optional blocked-reason tooltip when disabled.
	/// Enabled tooltips stay scene-authored; never pass happy-path tip text here.
	/// </summary>
	public readonly record struct ControlAvailability(bool Enabled, string BlockedReason = null);

	public static void LogWarn(string area, string message) => GD.PushWarning($"[IBT/{area}] {message}");
}

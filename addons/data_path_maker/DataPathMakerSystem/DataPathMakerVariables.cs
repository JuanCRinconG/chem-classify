/// <summary>
/// Data Path Maker strings and bootstrap paths.
/// Scene-authored chrome (including enabled tooltips) stays in the .tscn.
/// Prefix const is available outside TOOLS so Export hint strings can compose it.
/// </summary>
public static class DataPathMakerVariables
{
	public const string PopupScenePath =
		"res://addons/data_path_maker/DataPathMakerUI/DataPathPopup.tscn";

	/// <summary>Export hint-string prefix for Data Path Maker pickers (<c>DataPicker/MethodName</c>).</summary>
	public const string DataPicker = "DataPicker/";

	public const string DataNameLabelNone = "(no data)";
	public const string DataNameLabelUnsaved = "(unsaved)";

	public const string ChangeDataDialogTitle = "Select Data Path Resource";
	public const string ChangeDataDialogFilter = "Resource";

	public const string PickerHintNoBinding = "(no binding)";
	public const string PickerHintSessionUnavailable =
		"No session resource. Use Change Data to load a Resource root.";
	public const string PickerHintQueryUnresolved =
		"Picker query method could not be resolved.";
	public const string PickerHintQueryFailed =
		"Picker query failed against the current session Resource.";
}

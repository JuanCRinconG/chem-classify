#if TOOLS
using System;
using Godot;

/// <summary>
/// Inspector plugin that injects <see cref="DataPickerProperty"/> for DataPicker Export hint strings.
/// Owns the editor-lifetime Data Path Maker session root (any <see cref="Resource"/>).
/// Session usability is decided by the picker's instance query on the session root type.
/// </summary>
[Tool]
public partial class DataPathMakerInspector : EditorInspectorPlugin
{
	/// <summary>Live Resource queried by path pickers; null until InitialData seed or Change Data.</summary>
	public Resource SessionRoot { get; private set; }

	public event Action SessionRootChanged;

	public void SetSessionRoot(Resource root)
	{
		if (SessionRoot == root)
		{
			return;
		}

		SessionRoot = root;
		SessionRootChanged?.Invoke();
	}

	public void ClearSessionRoot()
	{
		if (SessionRoot == null)
		{
			return;
		}

		SessionRoot = null;
		SessionRootChanged?.Invoke();
	}

	public override bool _CanHandle(GodotObject @object)
	{
		return true;
	}

	public override bool _ParseProperty(
		GodotObject @object,
		Variant.Type type,
		string name,
		PropertyHint hintType,
		string hintString,
		PropertyUsageFlags usageFlags,
		bool wide)
	{
		if (@object == null)
		{
			return false;
		}

		if (!DataPickerSite.TryFromHintString(hintType, hintString, out var picker))
		{
			return false;
		}

		AddPropertyEditor(name, new DataPickerProperty(picker));
		return true;
	}
}

/// <summary>Editor property button + path-navigator popup for a single DataPicker Export field.</summary>
[Tool]
public partial class DataPickerProperty : EditorProperty
{
	private readonly DataPickerSite _picker;
	private readonly Button _pickerButton = new();
	private readonly DataPathPopup _popup;

	/// <summary>
	/// Parameterless ctor required by Godot's ScriptManagerBridge when reloading [Tool] scripts.
	/// Inspector wiring uses <see cref="DataPickerProperty(DataPickerSite)"/>.
	/// </summary>
	public DataPickerProperty()
		: this(default)
	{
	}

	public DataPickerProperty(DataPickerSite picker)
	{
		_picker = picker;

		_pickerButton.Flat = true;
		_pickerButton.Alignment = HorizontalAlignment.Left;
		_pickerButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_pickerButton.Pressed += OnPickerPressed;
		AddChild(_pickerButton);

		_popup = DataPathPopup.Create();
		AddChild(_popup);
	}

	public override void _EnterTree()
	{
		base._EnterTree();
		_popup.Hide();

		var inspector = DataPathMakerPlugin.CurrentInstance?.InspectorPlugin;
		if (inspector != null)
		{
			inspector.SessionRootChanged += OnSessionRootChanged;
		}
	}

	public override void _ExitTree()
	{
		var inspector = DataPathMakerPlugin.CurrentInstance?.InspectorPlugin;
		if (inspector != null)
		{
			inspector.SessionRootChanged -= OnSessionRootChanged;
		}

		base._ExitTree();
	}

	private void OnSessionRootChanged()
	{
		RefreshPickerLabel();
	}

	public override void _UpdateProperty()
	{
		RefreshPickerLabel();
	}

	private StringName ReadCurrentValue()
	{
		var owner = GetEditedObject();
		var propertyName = GetEditedProperty().ToString();
		if (owner == null || string.IsNullOrEmpty(propertyName))
		{
			return default;
		}

		var variant = owner.Get(propertyName);
		return variant.VariantType == Variant.Type.Nil
			? default
			: DataPathOperations.Normalize(variant.AsStringName());
	}

	/// <summary>
	/// Hard gates only (owner / query method name). Missing session or query failure still allows
	/// opening the popup so Change Data can fix the session.
	/// </summary>
	private bool HasHardPrerequisites(out string disabledHint)
	{
		disabledHint = string.Empty;
		var owner = GetEditedObject();
		if (owner == null)
		{
			disabledHint = DataPathMakerVariables.PickerHintNoBinding;
			return false;
		}

		if (!_picker.IsValid)
		{
			disabledHint = DataPathMakerVariables.PickerHintQueryUnresolved;
			return false;
		}

		return true;
	}

	private void RefreshPickerLabel()
	{
		if (!HasHardPrerequisites(out var disabledHint))
		{
			_pickerButton.Text = disabledHint;
			_pickerButton.Disabled = true;
			return;
		}

		_pickerButton.Disabled = false;

		var value = ReadCurrentValue();
		_pickerButton.Text = DataPathOperations.IsEmpty(value) || value == DataPathOperations.NoneTag
			? DataPathOperations.NoneTag.ToString()
			: value.ToString();
	}

	private void OnPickerPressed()
	{
		var owner = GetEditedObject();
		if (owner == null || !HasHardPrerequisites(out _))
		{
			return;
		}

		_popup.Show(
			_picker,
			owner,
			OnPathCommitted);
	}

	private void OnPathCommitted(StringName path, bool isFinal)
	{
		var owner = GetEditedObject();
		if (owner == null)
		{
			return;
		}

		WriteStringName(owner, GetEditedProperty(), path, isFinal);
		RefreshPickerLabel();
	}

	private void WriteStringName(GodotObject owner, StringName property, StringName value, bool isFinal)
	{
		var normalized = DataPathOperations.Normalize(value);
		var text = DataPathOperations.IsEmpty(normalized) ? string.Empty : normalized.ToString();
		// Intermediate commits must not rebuild the inspector or the popup is dismissed mid-query.
		EmitChanged(property, text, field: default, changing: !isFinal);
		owner.Set(property, normalized);
	}
}
#endif

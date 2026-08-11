#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Data Path Maker popup: session toolbar, search/back workspace, and failure space.
/// Authored in DataPathPopup.tscn — one script, no chrome bridge events.
/// </summary>
[Tool]
public partial class DataPathPopup : PopupPanel
{
	[Export]
	public Resource InitialData;

	/// <summary>
	/// Offset from the mouse cursor when opening (screen space). Editable on the .tscn.
	/// </summary>
	[Export]
	public Vector2 PopupMouseOffset;

	[Export]
	public Control ChromeRoot;

	[Export]
	public Control FailureSpace;

	[Export]
	public Control DataPathMakerWorkspace;

	[Export]
	public Button ChangeDataButton;

	[Export]
	public Label CurrentDataLabel;

	[Export]
	public Button BackButton;

	[Export]
	public LineEdit Search;

	[Export]
	public ItemList KeyList;

	private bool _uiBound;
	private bool _hasSessionRoot;
	private string _failureReason;

	private DataPickerSite _picker;
	private GodotObject _owner;
	private Action<StringName, bool> _onCommitted;
	private readonly List<StringName> _editable = new();

	private DataQueryResult _currentResult;
	private bool _hasCurrentResult;
	private bool _resolved;

	/// <summary>Instantiates <c>DataPathPopup.tscn</c>.</summary>
	public static DataPathPopup Create()
	{
		return GD.Load<PackedScene>(DataPathMakerVariables.PopupScenePath)
			.Instantiate<DataPathPopup>();
	}

	/// <summary>Opens the path navigator for a DataPicker inspector field.</summary>
	/// <param name="onCommitted">
	/// Invoked on each navigation commit. Second argument is <c>true</c> for a final write
	/// (terminal pick or None); <c>false</c> for intermediate steps so the inspector can avoid rebuild.
	/// </param>
	public void Show(
		DataPickerSite picker,
		GodotObject owner,
		Action<StringName, bool> onCommitted)
	{
		_picker = picker;
		_owner = owner;
		_onCommitted = onCommitted;
		ResetNavigation();

		BindUi();
		TrySeedSessionFromInitialData();
		RefreshDataChrome();

		var queryFailure = ResolveAndPrefill();
		var hasRoot = DataPathMakerPlugin.CurrentInstance.InspectorPlugin.SessionRoot != null;
		ApplyDataStoreGate(hasRoot, _resolved ? null : queryFailure);

		PlaceAtMouse();
		Popup();

		if (!_hasSessionRoot || !string.IsNullOrEmpty(_failureReason))
		{
			return;
		}

		PrepareSearch();
		RefreshChrome();
	}

	private void PlaceAtMouse()
	{
		var mouse = DisplayServer.MouseGetPosition();
		Position = mouse + (Vector2I)PopupMouseOffset;
	}

	private void BindUi()
	{
		if (_uiBound)
		{
			return;
		}

		Search.TextChanged += OnSearchFilterChanged;
		KeyList.ItemSelected += OnItemSelected;
		BackButton.Pressed += OnBackPressed;
		ChangeDataButton.Pressed += OpenChangeDataDialog;
		_uiBound = true;
	}

	private void ResetNavigation()
	{
		_editable.Clear();
		_hasCurrentResult = false;
		_resolved = false;
	}

	private bool TrySeedSessionFromInitialData()
	{
		var inspector = DataPathMakerPlugin.CurrentInstance.InspectorPlugin;
		if (inspector.SessionRoot != null || InitialData == null)
		{
			return false;
		}

		inspector.SetSessionRoot(InitialData);
		return true;
	}

	private void RefreshDataChrome()
	{
		var root = DataPathMakerPlugin.CurrentInstance.InspectorPlugin.SessionRoot;
		if (root == null || !GodotObject.IsInstanceValid(root))
		{
			CurrentDataLabel.Text = DataPathMakerVariables.DataNameLabelNone;
			return;
		}

		var resourcePath = root.ResourcePath;
		CurrentDataLabel.Text = string.IsNullOrEmpty(resourcePath)
			? DataPathMakerVariables.DataNameLabelUnsaved
			: resourcePath.GetFile();
	}

	private void ApplyDataStoreGate(bool hasDataStore, string failureReason = null)
	{
		_hasSessionRoot = hasDataStore;
		_failureReason = failureReason;

		var canAuthor = _hasSessionRoot && string.IsNullOrEmpty(_failureReason);
		DataPathMakerWorkspace.Visible = canAuthor;
		FailureSpace.Visible = !canAuthor;
		if (canAuthor)
		{
			return;
		}

		ApplyFailureReason(!string.IsNullOrEmpty(_failureReason)
			? _failureReason
			: DataPathMakerVariables.PickerHintSessionUnavailable);
	}

	private void ApplyFailureReason(string reason)
	{
		foreach (var child in FailureSpace.GetChildren())
		{
			if (child is Label label)
			{
				label.Text = reason;
				return;
			}
		}

		GD.PushWarning($"[DataPathMaker] {reason}");
	}

	private void PrepareSearch()
	{
		Search.PlaceholderText = "Search…";
		Search.Text = string.Empty;
		Search.GrabFocus();
	}

	private void RefreshChrome()
	{
		BackButton.Disabled = _editable.Count == 0;
		PushKeys(Search.Text ?? string.Empty);
	}

	private void PushKeys(string filter)
	{
		var data = DataPathMakerPlugin.CurrentInstance.InspectorPlugin.SessionRoot;
		if (data == null || !_resolved)
		{
			return;
		}

		if (!_picker.TryInvoke(data, _editable, out _currentResult, out _))
		{
			_hasCurrentResult = false;
			ApplyDataStoreGate(true, _currentResult.FailureReason
				?? DataPathMakerVariables.PickerHintQueryFailed);
			return;
		}

		_hasCurrentResult = true;
		ShowKeys(_currentResult.NextKeys, filter);
	}

	private void ShowKeys(IReadOnlyList<StringName> keys, string filter)
	{
		KeyList.Clear();
		var filterLower = filter?.Trim().ToLowerInvariant() ?? string.Empty;

		if (string.IsNullOrEmpty(filterLower)
			|| DataPathOperations.NoneTag.ToString().Contains(filterLower, StringComparison.OrdinalIgnoreCase))
		{
			AddSelectableKey(DataPathOperations.NoneTag.ToString());
		}

		if (keys == null || keys.Count == 0)
		{
			AddHintItem("No registered keys.");
			return;
		}

		var anyVisible = false;
		for (var i = 0; i < keys.Count; i++)
		{
			var key = keys[i];
			if (DataPathOperations.IsEmpty(key))
			{
				continue;
			}

			var trimmed = key.ToString();
			if (!string.IsNullOrEmpty(filterLower)
				&& !trimmed.Contains(filterLower, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			anyVisible = true;
			AddSelectableKey(trimmed);
		}

		if (!anyVisible)
		{
			AddHintItem("No matching keys.");
		}
	}

	private void OpenChangeDataDialog()
	{
		var dialog = new EditorFileDialog
		{
			FileMode = EditorFileDialog.FileModeEnum.OpenFile,
			Access = EditorFileDialog.AccessEnum.Resources,
			Title = DataPathMakerVariables.ChangeDataDialogTitle,
		};
		dialog.AddFilter("*.tres", DataPathMakerVariables.ChangeDataDialogFilter);
		dialog.AddFilter("*.res", DataPathMakerVariables.ChangeDataDialogFilter);
		dialog.FileSelected += path =>
		{
			ApplySessionFromPath(path);
			dialog.QueueFree();
		};
		dialog.Canceled += dialog.QueueFree;
		AddChild(dialog);
		dialog.PopupFileDialog();
	}

	private void ApplySessionFromPath(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return;
		}

		var loaded = ResourceLoader.Load<Resource>(path);
		if (loaded == null || !GodotObject.IsInstanceValid(loaded))
		{
			GD.PushWarning($"[DataPathMaker] Could not load Resource from '{path}'.");
			return;
		}

		DataPathMakerPlugin.CurrentInstance.InspectorPlugin.SetSessionRoot(loaded);
		HandleSessionRootChanged();
	}

	private void HandleSessionRootChanged()
	{
		ResetNavigation();
		RefreshDataChrome();

		var queryFailure = ResolveAndPrefill();
		var hasRoot = DataPathMakerPlugin.CurrentInstance.InspectorPlugin.SessionRoot != null;
		ApplyDataStoreGate(hasRoot, _resolved ? null : queryFailure);
		if (!hasRoot || !_resolved)
		{
			return;
		}

		PrepareSearch();
		RefreshChrome();
	}

	private string ResolveAndPrefill()
	{
		_resolved = false;
		_hasCurrentResult = false;
		var store = DataPathMakerPlugin.CurrentInstance.InspectorPlugin.SessionRoot;
		if (store == null)
		{
			return DataPathMakerVariables.PickerHintSessionUnavailable;
		}

		if (!_picker.TryResolve(store.GetType(), out var failureReason))
		{
			return failureReason;
		}

		_resolved = true;
		PrefillEditableFromOwnerPath();
		return null;
	}

	private void PrefillEditableFromOwnerPath()
	{
		var existingPath = TryReadOwnerPath(_owner);
		if (DataPathOperations.IsEmpty(existingPath))
		{
			return;
		}

		var store = DataPathMakerPlugin.CurrentInstance.InspectorPlugin.SessionRoot;
		if (store == null || !_resolved)
		{
			return;
		}

		var existing = DataPathOperations.SplitPath(existingPath);
		for (var i = 0; i < existing.Count; i++)
		{
			if (!_picker.TryInvoke(store, _editable, out var step, out _))
			{
				break;
			}

			// Leave the terminal tip for the user to re-pick; half-finished paths stop here
			// with every committed non-terminal segment already in _editable.
			if (step.IsTerminalStep)
			{
				break;
			}

			var segment = DataPathOperations.Normalize(existing[i]);
			if (!ContainsKey(step.NextKeys, segment))
			{
				break;
			}

			_editable.Add(segment);
		}
	}

	private static bool ContainsKey(Godot.Collections.Array<StringName> keys, StringName segment)
	{
		if (keys == null || DataPathOperations.IsEmpty(segment))
		{
			return false;
		}

		for (var i = 0; i < keys.Count; i++)
		{
			if (DataPathOperations.KeysEqual(keys[i], segment))
			{
				return true;
			}
		}

		return false;
	}

	private static StringName TryReadOwnerPath(GodotObject owner)
	{
		if (owner == null)
		{
			return default;
		}

		var variant = owner.Get("Path");
		if (variant.VariantType == Variant.Type.Nil)
		{
			return default;
		}

		StringName raw = variant.VariantType switch
		{
			Variant.Type.StringName => variant.AsStringName(),
			Variant.Type.String => new StringName(variant.AsString()),
			_ => default,
		};

		return DataPathOperations.JoinPath(DataPathOperations.SplitPath(raw));
	}

	private void OnBackPressed()
	{
		if (_editable.Count == 0)
		{
			return;
		}

		_editable.RemoveAt(_editable.Count - 1);
		_hasCurrentResult = false;
		CommitCurrentPath(isFinal: false);
		RefreshChrome();
	}

	private void OnItemSelected(long index)
	{
		if (index < 0 || index >= KeyList.ItemCount)
		{
			return;
		}

		var itemIndex = (int)index;
		if (KeyList.IsItemDisabled(itemIndex))
		{
			return;
		}

		var selectedId = KeyList.GetItemMetadata(itemIndex).AsStringName();
		if (DataPathOperations.IsEmpty(selectedId))
		{
			return;
		}

		HandleKeyChosen(selectedId);
	}

	private void HandleKeyChosen(StringName key)
	{
		if (key == DataPathOperations.NoneTag)
		{
			_onCommitted?.Invoke(default, true);
			Hide();
			return;
		}

		var terminal = _hasCurrentResult && _currentResult.IsTerminalStep;
		_editable.Add(DataPathOperations.Normalize(key));
		CommitCurrentPath(isFinal: terminal);
		if (terminal)
		{
			Hide();
			return;
		}

		_hasCurrentResult = false;
		Callable.From(RefreshChrome).CallDeferred();
	}

	/// <summary>
	/// Writes the current editable path to the bound field (partial or complete).
	/// Intermediate commits use <c>changing: true</c> so the inspector does not rebuild and dismiss the popup.
	/// </summary>
	private void CommitCurrentPath(bool isFinal)
	{
		_onCommitted?.Invoke(
			_editable.Count == 0
				? default
				: DataPathOperations.JoinPath(_editable),
			isFinal);
	}

	private void OnSearchFilterChanged(string newText) => PushKeys(newText);

	private void AddHintItem(string message)
	{
		var index = KeyList.AddItem(message);
		KeyList.SetItemDisabled(index, true);
		KeyList.SetItemSelectable(index, false);
	}

	private void AddSelectableKey(string key)
	{
		var index = KeyList.AddItem(key);
		KeyList.SetItemMetadata(index, key);
	}
}
#endif

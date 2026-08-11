#if TOOLS
using System;
using Godot;

namespace IBTPlugin;

/// <summary>
/// Main-screen brain for <c>ChartCreator.tscn</c>: session behavior data + chart,
/// FailureSpace / AuthoringWorkspace gate, data picker, Refresh Data.
/// </summary>
[Tool]
public partial class IBTChartCreatorUI : VBoxContainer
{
	private IBTBindings _uiBindings = new();
	private bool _suspendDataPickerHandler;

	/// <summary>Singular live UI while the Internal Behavior Tools plugin is enabled.</summary>
	public static IBTChartCreatorUI CurrentInstance { get; private set; }

	/// <summary>Authoring-session behavior data; seeded from <see cref="InitialData"/> or the data picker.</summary>
	public IBTData BehaviorData { get; private set; }

	/// <summary>Gets the session behavior data only when it is available and its scan is clean.</summary>
	public static bool TryGetHealthyData(out IBTData data)
	{
		data = CurrentInstance?.BehaviorData;
		return data != null && !data.HasScanIssues;
	}

	/// <summary>Optional default behavior data loaded when the main screen enters the tree.</summary>
	[Export]
	public IBTData InitialData;

	[Export]
	public Label TitleLabel;

	[Export]
	public EditorResourcePicker DataPicker;

	[Export]
	public Button RefreshDataButton;

	[Export]
	public Control FailureSpace;

	[Export]
	public Label FailureTitle;

	[Export]
	public Label FailureHint;

	[Export]
	public ItemList IssuesList;

	[Export]
	public Control AuthoringWorkspace;

	[Export]
	public BehaviorGraphEditor GraphView;

	public override void _EnterTree()
	{
		CurrentInstance = this;
		if (InitialData != null)
		{
			BehaviorData = InitialData;
			IBTManager.RefreshData(BehaviorData);
		}
	}

	public override void _Ready()
	{
		DataPicker.BaseType = nameof(IBTData);
		BindUINodes();
		SyncDataPickerFromSession();
		ApplyDockFromState();
	}

	public override void _ExitTree()
	{
		_uiBindings.Clear();
		CurrentInstance = null;
	}

	public void BindUINodes()
	{
		_uiBindings.Clear();
		_uiBindings.Bind<EditorResourcePicker.ResourceChangedEventHandler>(
			add => DataPicker.ResourceChanged += add,
			remove => DataPicker.ResourceChanged -= remove,
			OnDataResourceChanged);
		_uiBindings.BindButton(RefreshDataButton, RefreshSessionData);
	}

	/// <summary>
	/// Behavior-data health controls the failure notice and gates all chart authoring controls.
	/// </summary>
	public void ApplyDockFromState()
	{
		ApplyRefreshDataAvailability();

		BehaviorScanGatePresenter.Apply(
			FailureSpace,
			FailureTitle,
			FailureHint,
			IssuesList,
			AuthoringWorkspace,
			BehaviorData,
			new ScanGateLabels
			{
				NoDataTitle = IBTVariables.FailureTitleNoData,
				NoDataHint = IBTVariables.FailureHintNoData,
				DirtyTitle = IBTVariables.FailureTitleDirty,
				DirtyHint = IBTVariables.FailureHintDirty,
			});

		GraphView.RefreshDataContext();
		SyncDataPickerFromSession();
	}

	public void RefreshSessionData()
	{
		if (!BehaviorData.IsValid())
		{
			ApplyDockFromState();
			return;
		}

		IBTManager.RefreshData(BehaviorData);
		ApplyDockFromState();
	}

	private void SyncDataPickerFromSession()
	{
		_suspendDataPickerHandler = true;
		DataPicker.EditedResource = BehaviorData;
		_suspendDataPickerHandler = false;
	}

	private void OnDataResourceChanged(Resource resource)
	{
		if (_suspendDataPickerHandler)
		{
			return;
		}

		if (resource != null && resource is not IBTData)
		{
			IBTVariables.LogWarn(
				"Data",
				$"Expected {nameof(IBTData)}, got '{resource.GetClass()}'.");
			SyncDataPickerFromSession();
			return;
		}

		BehaviorData = resource as IBTData;
		if (BehaviorData != null)
		{
			IBTManager.RefreshData(BehaviorData);
		}

		ApplyDockFromState();
	}

	private void ApplyRefreshDataAvailability()
	{
		bool hasData = BehaviorData.IsValid();
		IBTEditorHelpers.ApplyControlAvailability(
			RefreshDataButton,
			hasData
				? new IBTVariables.ControlAvailability(true)
				: new IBTVariables.ControlAvailability(
					false,
					IBTVariables.EmptyDataHint));
	}
}
#endif

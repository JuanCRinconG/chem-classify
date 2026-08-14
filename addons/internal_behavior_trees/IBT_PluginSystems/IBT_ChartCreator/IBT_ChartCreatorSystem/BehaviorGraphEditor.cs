#if TOOLS
#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using IBTSystem;

namespace IBTPlugin;

/// <summary>
/// Chart authoring GraphEdit shell. Composes a shared graph surface and owns toolbar CRUD.
/// </summary>
[Tool]
public partial class BehaviorGraphEditor : GraphEdit
{
	internal static readonly StringName InitialBehaviorInstanceIdMeta =
		BehaviorGraphNodeLookup.InitialBehaviorInstanceIdMeta;

	public static BehaviorGraphEditor? CurrentInstance { get; private set; }

	private readonly IBTBindings _uiBindings = new();
	private readonly BehaviorGraphChartState _chartState = new();

	private BehaviorGraphSurface _surface = null!;
	private IBTChart? _chart;
	private bool _suspendChartPickerHandler;

	/// <summary>The chart currently authored by this graph editor.</summary>
	public IBTChart? ActiveChart
	{
		get => _chart;
		set
		{
			if (_chart != value)
			{
				LoadChart(value);
			}
		}
	}

	[Export]
	public IBTChart InitialChart = null!;

	[Export]
	public PackedScene BehaviorNodeScene = null!;

	[Export]
	public PackedScene TransitionEngineNodeScene = null!;

	[Export]
	public PackedScene ChangeChartNodeTypePopupScene = null!;

	[Export]
	public PackedScene RenameBindingGroupPopupScene = null!;

	[Export]
	public OptionButton GroupSelector = null!;

	[Export]
	public OptionButton BehaviorSelector = null!;

	[Export]
	public Button AddBehaviorNodeButton = null!;

	[Export]
	public OptionButton EmitterGroupSelector = null!;

	[Export]
	public OptionButton EmitterSelector = null!;

	[Export]
	public Button AddTransitionEngineButton = null!;

	[Export]
	public EditorResourcePicker ChartPicker = null!;

	public override void _EnterTree()
	{
		_chart = InitialChart;
		CurrentInstance = this;
	}

	public override void _Ready()
	{
		_surface = BehaviorGraphSurface.Attach(
			this,
			_chartState,
			new BehaviorGraphSurfaceOptions
			{
				BehaviorNodeScene = BehaviorNodeScene,
				TransitionEngineNodeScene = TransitionEngineNodeScene,
				GetBehaviorData = GetAuthoringBehaviorData,
				CommitHandler = chart => BehaviorChartPersistence.TryPersistToDisk(chart),
				OnChildWireTopologyChanged = (source, previousChildCount) =>
					source.ApplyAutomaticProcessOrderForChildCount(previousChildCount),
			});
		_surface.ConfigureConnectionTypes();
		BindUINodes();
		BindChartPicker();
		SyncChartPickerFromSession();
		LoadChart(_chart);
	}

	public void BindUINodes()
	{
		_uiBindings.Clear();
		_surface.BindInteractionSignals(_uiBindings);
		_uiBindings.BindOptionButton(GroupSelector, _ =>
			IBTEditorHelpers.PopulateBehaviorSelectorForGroup(GroupSelector, BehaviorSelector));
		_uiBindings.BindOptionButton(BehaviorSelector, _ => RefreshActionAvailability());
		_uiBindings.BindButton(AddBehaviorNodeButton, AddSelectedBehavior);
		_uiBindings.BindOptionButton(EmitterGroupSelector, _ =>
			IBTEditorHelpers.PopulateEmitterSelectorForGroup(EmitterGroupSelector, EmitterSelector));
		_uiBindings.BindOptionButton(EmitterSelector, _ => RefreshActionAvailability());
		_uiBindings.BindButton(AddTransitionEngineButton, AddSelectedTransitionEngine);
		_uiBindings.BindSignal(
			this,
			GraphEdit.SignalName.DeleteNodesRequest,
			Callable.From((Godot.Collections.Array<StringName> graphNodeNames) =>
				DeleteGraphNodes(graphNodeNames)));
	}

	public override void _ExitTree()
	{
		_uiBindings.Clear();
		_surface.Teardown();
		if (CurrentInstance == this)
		{
			CurrentInstance = null;
		}
	}

	public void SetInitialBehavior(string instanceId) => _surface.SetInitialBehavior(instanceId);

	public void RequestDeleteBehavior(string instanceId) =>
		RemoveChartNodes(new[] { instanceId }, Array.Empty<string>());

	public void RequestDeleteTransitionEngine(string instanceId) =>
		RemoveChartNodes(Array.Empty<string>(), new[] { instanceId });

	public void RequestChangeBehaviorType(string instanceId, string behaviorName)
	{
		if (_chart == null
			|| string.IsNullOrWhiteSpace(instanceId)
			|| string.IsNullOrWhiteSpace(behaviorName)
			|| !IBTChartCreatorUI.TryGetHealthyData(out IBTData data)
			|| !data.HasBehavior(behaviorName))
		{
			return;
		}

		if (!_chart.TryChangeBehaviorType(
			instanceId,
			behaviorName,
			data.GetBehaviorConfigTypeName(behaviorName),
			data.GetTransitionsFromBehavior(behaviorName)))
		{
			return;
		}

		AfterChartWorkspaceMutation(() => IncrementalRefreshBehavior(instanceId));
	}

	public void RequestChangeTransitionEngineType(string instanceId, string newEmitterTypeName)
	{
		if (_chart == null
			|| string.IsNullOrWhiteSpace(instanceId)
			|| string.IsNullOrWhiteSpace(newEmitterTypeName)
			|| !IBTChartCreatorUI.TryGetHealthyData(out IBTData data)
			|| !data.HasEmitter(newEmitterTypeName))
		{
			return;
		}

		if (!_chart.TryChangeTransitionEngineType(
			instanceId,
			newEmitterTypeName,
			data.GetTransitionsFromEmitter(newEmitterTypeName)))
		{
			return;
		}

		AfterChartWorkspaceMutation(() => IncrementalRefreshTransitionEngine(instanceId));
	}

	public void RequestRenameBoardGroup(string? fromGroup, string? toGroup)
	{
		if (_chart == null || !_chart.TryRenameBoardGroup(fromGroup, toGroup))
		{
			return;
		}

		AfterChartWorkspaceMutation(() => _surface.RefreshFromData());
	}

	public bool HasSelectedGraphNodes()
	{
		foreach (Node child in GetChildren())
		{
			if (child is GraphNode { Selected: true })
			{
				return true;
			}
		}

		return false;
	}

	public void DeleteSelectedGraphNodes()
	{
		var graphNodeNames = new Godot.Collections.Array<StringName>();
		foreach (Node child in GetChildren())
		{
			if (child is GraphNode { Selected: true } node)
			{
				graphNodeNames.Add(node.Name);
			}
		}

		if (graphNodeNames.Count > 0)
		{
			DeleteGraphNodes(graphNodeNames);
		}
	}

	public void CommitNodeMutation() => _surface.CommitChart();

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true })
		{
			GrabFocus();
		}
	}

	public void LoadChart(IBTChart? chart)
	{
		_chart = chart;
		_chartState.Chart = chart;
		RefreshSelectors();
		_surface.LoadChart();
		SyncChartPickerFromSession();
	}

	public void RefreshDataContext()
	{
		IBTEditorHelpers.PopulateBehaviorGroupSelector(GroupSelector);
		IBTEditorHelpers.PopulateEmitterGroupSelector(EmitterGroupSelector);
		_surface.RefreshFromData();
	}

	private void RefreshSelectors()
	{
		RepopulateSelectors();
		RefreshActionAvailability();
	}

	private void RepopulateSelectors()
	{
		IBTEditorHelpers.PopulateBehaviorGroupSelector(GroupSelector);
		IBTEditorHelpers.PopulateBehaviorSelectorForGroup(GroupSelector, BehaviorSelector);
		IBTEditorHelpers.PopulateEmitterGroupSelector(EmitterGroupSelector);
		IBTEditorHelpers.PopulateEmitterSelectorForGroup(EmitterGroupSelector, EmitterSelector);
	}

	private void DeleteGraphNodes(Godot.Collections.Array<StringName> graphNodeNames)
	{
		if (_chartState.IsSyncingGraph || _chart == null || graphNodeNames.Count == 0)
		{
			return;
		}

		var behaviorInstanceIds = new List<string>();
		var transitionEngineInstanceIds = new List<string>();
		for (int i = 0; i < graphNodeNames.Count; i++)
		{
			var graphNodePath = new NodePath(graphNodeNames[i]);
			if (GetNodeOrNull<IBTBehaviorNode>(graphNodePath) is IBTBehaviorNode behaviorGraphNode
				&& !string.IsNullOrEmpty(behaviorGraphNode.InstanceId))
			{
				behaviorInstanceIds.Add(behaviorGraphNode.InstanceId);
			}
			else if (GetNodeOrNull<IBTTransitionNode>(graphNodePath) is IBTTransitionNode
				{ InstanceId: { Length: > 0 } instanceId } engineGraphNode
				&& engineGraphNode is not IBTBehaviorNode)
			{
				transitionEngineInstanceIds.Add(instanceId);
			}
		}

		RemoveChartNodes(behaviorInstanceIds, transitionEngineInstanceIds);
	}

	private void RemoveChartNodes(
		IReadOnlyList<string> behaviorInstanceIds,
		IReadOnlyList<string> transitionEngineInstanceIds)
	{
		if (_chart == null)
		{
			return;
		}

		List<string> behaviorIds = CollectNonEmptyIds(behaviorInstanceIds);
		List<string> engineInstanceIds = CollectNonEmptyIds(transitionEngineInstanceIds);
		if (behaviorIds.Count == 0 && engineInstanceIds.Count == 0)
		{
			return;
		}

		var previousChildCounts = new Dictionary<string, int>(StringComparer.Ordinal);
		bool chartChanged = _chart.RemoveBehaviorNodes(behaviorIds, previousChildCounts) > 0;

		for (int i = 0; i < engineInstanceIds.Count; i++)
		{
			chartChanged |= _chart.TryRemoveTransitionEngineNode(engineInstanceIds[i]);
		}

		if (!chartChanged)
		{
			return;
		}

		ApplyAutomaticProcessOrderForAffectedParents(previousChildCounts);
		AfterChartWorkspaceMutation(
			() =>
			{
				for (int i = 0; i < behaviorIds.Count; i++)
				{
					RemoveGraphNodeIfPresent(BehaviorGraphNodeLookup.GetBehaviorNode(this, behaviorIds[i]));
				}

				for (int i = 0; i < engineInstanceIds.Count; i++)
				{
					RemoveGraphNodeIfPresent(
						BehaviorGraphNodeLookup.GetTransitionEngineNode(this, engineInstanceIds[i]));
				}
			},
			refreshInitial: behaviorIds.Count > 0);
	}

	private static List<string> CollectNonEmptyIds(IReadOnlyList<string> ids)
	{
		var collected = new List<string>();
		for (int i = 0; i < ids.Count; i++)
		{
			if (!string.IsNullOrWhiteSpace(ids[i]))
			{
				collected.Add(ids[i]);
			}
		}

		return collected;
	}

	private void RefreshActionAvailability()
	{
		bool hasChart = _chart.IsValid();
		bool hasBehaviorGroup = GroupSelector.TryGetSelectedMetadata(out _);
		bool hasBehavior = BehaviorSelector.TryGetSelectedMetadata(out _);
		bool hasEmitterGroup = EmitterGroupSelector.TryGetSelectedMetadata(out _);
		bool hasEmitter = EmitterSelector.TryGetSelectedMetadata(out _);
		IBTChartCreatorUI.TryGetHealthyData(out IBTData data);
		bool hasAnyEmitters = BehaviorChartAvailabilityQueries.HasAnyDataEmitters(data);

		IBTEditorHelpers.ApplyControlAvailability(
			AddBehaviorNodeButton,
			hasChart && hasBehaviorGroup && hasBehavior
				? new IBTVariables.ControlAvailability(true)
				: new IBTVariables.ControlAvailability(
					false,
					!hasChart
						? IBTVariables.NoActiveChartTooltip
						: IBTVariables.AddBehaviorNodeDisabledTooltip));
		IBTEditorHelpers.ApplyControlAvailability(
			AddTransitionEngineButton,
			hasChart && hasEmitterGroup && hasEmitter
				? new IBTVariables.ControlAvailability(true)
				: new IBTVariables.ControlAvailability(
					false,
					!hasChart
						? IBTVariables.NoActiveChartTooltip
						: hasAnyEmitters
							? IBTVariables.AddTransitionEngineNodeDisabledTooltip
							: IBTVariables.AddTransitionEngineDisabledTooltip));
	}

	private void AddSelectedBehavior()
	{
		if (_chart == null || !BehaviorSelector.TryGetSelectedMetadata(out string behaviorName))
		{
			return;
		}

		string instanceId = Guid.NewGuid().ToString("N");
		_chart.TryAddBehaviorNode(instanceId, new BehaviorNode { FullTypeName = behaviorName });
		AfterChartWorkspaceMutation(() => IncrementalAddBehavior(instanceId));
	}

	private void AddSelectedTransitionEngine()
	{
		if (_chart == null || !EmitterSelector.TryGetSelectedMetadata(out string emitterTypeName))
		{
			return;
		}

		string instanceId = Guid.NewGuid().ToString("N");
		var chartNode = new TransitionEngineNode
		{
			FullTypeName = emitterTypeName,
			BindingGroup = ChartBindingGroups.SuggestEmitterGroup(
				_chart,
				emitterTypeName,
				IBTVariables.SuggestedEngineGroupPrefix),
		};

		if (!_chart.TryAddTransitionEngineNode(instanceId, chartNode))
		{
			RefreshActionAvailability();
			return;
		}

		AfterChartWorkspaceMutation(() => IncrementalAddTransitionEngine(instanceId, chartNode));
	}

	private void AfterChartWorkspaceMutation(Action graphUpdate, bool refreshInitial = false)
	{
		_surface.CommitChart();
		RepopulateSelectors();
		graphUpdate?.Invoke();
		if (refreshInitial)
		{
			_surface.RefreshInitialBehaviorButtons();
		}

		RefreshActionAvailability();
	}

	private void ApplyAutomaticProcessOrderForAffectedParents(
		IReadOnlyDictionary<string, int> previousChildCountByParentId)
	{
		foreach (KeyValuePair<string, int> entry in previousChildCountByParentId)
		{
			if (BehaviorGraphNodeLookup.GetBehaviorNode(this, entry.Key) is IBTBehaviorNode parentGraphNode)
			{
				parentGraphNode.ApplyAutomaticProcessOrderForChildCount(entry.Value);
			}
		}
	}

	private static IBTData? GetAuthoringBehaviorData() =>
		IBTChartCreatorUI.TryGetHealthyData(out IBTData data) ? data : null;

	private void IncrementalAddBehavior(string instanceId)
	{
		IBTChart? chart = _chartState.Chart;
		if (chart == null
			|| string.IsNullOrWhiteSpace(instanceId)
			|| !chart.BehaviorNodes.TryGetValue(instanceId, out BehaviorNode? node)
			|| node == null)
		{
			return;
		}

		_surface.EnsureInitialBehaviorButtonGroup();
		int fallbackIndex = _surface.AllocateFallbackPositionIndex();
		_surface.InstantiateBehaviorNode(
			instanceId,
			node,
			BehaviorChartGraphLayout.ResolvePosition(node.Position, fallbackIndex),
			chart,
			GetAuthoringBehaviorData());
	}

	private void IncrementalAddTransitionEngine(string instanceId, TransitionEngineNode chartNode)
	{
		if (_chartState.Chart == null
			|| string.IsNullOrWhiteSpace(instanceId)
			|| chartNode == null)
		{
			return;
		}

		int fallbackIndex = _surface.AllocateFallbackPositionIndex();
		_surface.InstantiateTransitionEngineNode(
			instanceId,
			chartNode,
			BehaviorChartGraphLayout.ResolvePosition(
				chartNode.Position,
				_chartState.Chart.BehaviorNodes.Count + fallbackIndex),
			GetAuthoringBehaviorData());
	}

	private void IncrementalRefreshBehavior(string instanceId)
	{
		if (BehaviorGraphNodeLookup.GetBehaviorNode(this, instanceId) is not IBTBehaviorNode graphNode
			|| graphNode.TransitionNode == null)
		{
			return;
		}

		_surface.RefreshGraphNode(instanceId, graphNode, GetAuthoringBehaviorData());
	}

	private void IncrementalRefreshTransitionEngine(string instanceId)
	{
		if (BehaviorGraphNodeLookup.GetTransitionEngineNode(this, instanceId) is not IBTTransitionNode graphNode
			|| graphNode.TransitionNode == null)
		{
			return;
		}

		_surface.RefreshGraphNode(instanceId, graphNode, GetAuthoringBehaviorData());
	}

	private void RemoveGraphNodeIfPresent(GraphNode? node)
	{
		if (node != null)
		{
			_surface.RemoveGraphNode(node);
		}
	}

	private void BindChartPicker()
	{
		if (ChartPicker == null)
		{
			return;
		}

		ChartPicker.BaseType = nameof(IBTChart);
		_uiBindings.Bind<EditorResourcePicker.ResourceChangedEventHandler>(
			add => ChartPicker.ResourceChanged += add,
			remove => ChartPicker.ResourceChanged -= remove,
			OnChartResourceChanged);
	}

	private void SyncChartPickerFromSession()
	{
		if (ChartPicker == null)
		{
			return;
		}

		_suspendChartPickerHandler = true;
		ChartPicker.EditedResource = _chart;
		_suspendChartPickerHandler = false;
	}

	private void OnChartResourceChanged(Resource resource)
	{
		if (_suspendChartPickerHandler)
		{
			return;
		}

		if (resource != null && resource is not IBTChart)
		{
			IBTVariables.LogWarn(
				"Chart",
				$"Expected {nameof(IBTChart)}, got '{resource.GetClass()}'.");
			SyncChartPickerFromSession();
			return;
		}

		LoadChart(resource as IBTChart);
	}
}

/// <summary>Toolbar availability queries for the authoring graph editor.</summary>
static file class BehaviorChartAvailabilityQueries
{
	public static bool HasAnyDataEmitters(IBTData? data) =>
		data != null && data.GetEmitterNames().Length > 0;
}
#endif

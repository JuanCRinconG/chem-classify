#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTPlugin;

/// <summary>
/// Runtime wire-only behavior chart editor. Edits an in-memory chart copy until Push Graph.
/// </summary>
public partial class IBTRuntimeGraphEdit : GraphEdit
{
	private readonly IBTBindings _uiBindings = new();
	private readonly BehaviorGraphChartState _chartState = new();

	private BehaviorGraphSurface _surface = null!;
	private IBTChart? _workingChart;
	private EditableBehaviorChart? _activeHost;
	private BehaviorEngine? _subscribedEngine;
	private bool _topologySynced;
	private readonly HashSet<string> _highlightedInstanceIds = new(StringComparer.Ordinal);

	[Export]
	public PackedScene BehaviorNodeScene { get; set; } = null!;

	[Export]
	public PackedScene TransitionEngineNodeScene { get; set; } = null!;

	[Export]
	public OptionButton EntitySelector { get; set; } = null!;

	[Export]
	public Button PushGraphButton { get; set; } = null!;

	[Export]
	public Theme InactiveGraphNodeTheme { get; set; } = null!;

	[Export]
	public Theme ActiveGraphNodeTheme { get; set; } = null!;

	[Export]
	public VBoxContainer TransitionLabelsSpace { get; set; } = null!;

	[Export]
	public Label TransitionLabelDefault { get; set; } = null!;

	[Export]
	public float TransitionLabelHoldSeconds { get; set; } = 0.5f;

	[Export]
	public float TransitionLabelFadeSeconds { get; set; } = 2f;

	public override void _Ready()
	{
		_surface = BehaviorGraphSurface.Attach(
			this,
			_chartState,
			new BehaviorGraphSurfaceOptions
			{
				BehaviorNodeScene = BehaviorNodeScene,
				TransitionEngineNodeScene = TransitionEngineNodeScene,
				GetBehaviorData = GetRuntimeBehaviorData,
				CommitHandler = _ => { },
				OnChildWireTopologyChanged = (source, previousChildCount) =>
					source.ApplyAutomaticProcessOrderForChildCount(previousChildCount),
				OnChartTopologyChanged = OnWorkingChartTopologyChanged,
			});
		_surface.ConfigureConnectionTypes();
		_surface.BindInteractionSignals(_uiBindings);
		BindToolbar();
		UpdatePushButtonAvailability();
	}

	public override void _ExitTree()
	{
		UnsubscribeEngine();
		_uiBindings.Clear();
		_surface.Teardown();
	}

	public void OnDiscoveredEntitiesReady()
	{
		if (EntitySelector.ItemCount > 0)
		{
			if (EntitySelector.Selected < 0)
			{
				EntitySelector.Select(0);
			}

			LoadSelectedEntity();
			return;
		}

		ClearChart();
	}

	private void BindToolbar()
	{
		_uiBindings.BindOptionButton(EntitySelector, _ => LoadSelectedEntity());
		_uiBindings.BindButton(PushGraphButton, PushChartToHost);
	}

	private static IBTData? GetRuntimeBehaviorData()
	{
		IBTData data = IBTManager.GetRuntimeData();
		return data.IsValid() && !data.HasScanIssues ? data : null;
	}

	private void LoadSelectedEntity()
	{
		UnsubscribeEngine();
		ClearActiveHighlight();
		ClearTransitionLabels();

		int index = EntitySelector.Selected;
		if (index < 0 || index >= EntitySelector.ItemCount)
		{
			ClearChart();
			return;
		}

		if (EntitySelector.GetItemMetadata(index).AsGodotObject() is not EditableBehaviorChart host)
		{
			ClearChart();
			return;
		}

		host.GetRuntimeInfo(out IBTChart sourceChart, out BehaviorEngine engine);
		if (!sourceChart.IsValid())
		{
			ClearChart();
			return;
		}

		_activeHost = host;
		_workingChart = (IBTChart)sourceChart.Duplicate(true);
		_chartState.Chart = _workingChart;
		_surface.LoadChart();
		ApplyInactiveThemeToAllBehaviorNodes();
		SubscribeEngine(engine);
		_topologySynced = true;
		ApplyActiveHighlight(engine.ActiveBehaviorInstanceId);
		UpdatePushButtonAvailability();
	}

	private void PushChartToHost()
	{
		if (_activeHost == null || _workingChart == null)
		{
			return;
		}

		_topologySynced = true;
		_activeHost.SetChart(_workingChart);
		if (_subscribedEngine != null)
		{
			ApplyActiveHighlight(_subscribedEngine.ActiveBehaviorInstanceId);
		}
	}

	private void ClearChart()
	{
		UnsubscribeEngine();
		ClearActiveHighlight();
		ClearTransitionLabels();
		_activeHost = null;
		_workingChart = null;
		_topologySynced = false;
		_chartState.Chart = null;
		_surface.Teardown();
		UpdatePushButtonAvailability();
	}

	private void OnWorkingChartTopologyChanged()
	{
		_topologySynced = false;
		ClearActiveHighlight();
	}

	private void OnActiveBehaviorInstanceChanged(string instanceId)
	{
		if (!_topologySynced)
		{
			return;
		}

		ApplyActiveHighlight(instanceId);
	}

	private void OnTransitionOccurred(BehaviorTransitionEvent transitionEvent)
	{
		if (!_topologySynced)
		{
			return;
		}

		SpawnTransitionLabel(transitionEvent);
	}

	private void SubscribeEngine(BehaviorEngine engine)
	{
		UnsubscribeEngine();
		_subscribedEngine = engine;
		engine.ActiveBehaviorInstanceChanged += OnActiveBehaviorInstanceChanged;
		engine.TransitionOccurred += OnTransitionOccurred;
	}

	private void UnsubscribeEngine()
	{
		if (_subscribedEngine == null)
		{
			return;
		}

		_subscribedEngine.ActiveBehaviorInstanceChanged -= OnActiveBehaviorInstanceChanged;
		_subscribedEngine.TransitionOccurred -= OnTransitionOccurred;
		_subscribedEngine = null;
	}

	private void ClearTransitionLabels()
	{
		if (TransitionLabelsSpace == null)
		{
			return;
		}

		foreach (Node child in TransitionLabelsSpace.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void SpawnTransitionLabel(BehaviorTransitionEvent transitionEvent)
	{
		if (TransitionLabelDefault == null || TransitionLabelsSpace == null)
		{
			return;
		}

		var label = (Label)TransitionLabelDefault.Duplicate();
		label.Text = transitionEvent.MessageRedux;
		label.Visible = true;
		label.Modulate = TransitionLabelDefault.Modulate with { A = 1f };
		label.MouseFilter = MouseFilterEnum.Ignore;
		TransitionLabelsSpace.AddChild(label);

		Tween tween = label.CreateTween();
		tween.TweenInterval(TransitionLabelHoldSeconds);
		tween.TweenProperty(label, "modulate:a", 0f, TransitionLabelFadeSeconds);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private void ApplyInactiveThemeToAllBehaviorNodes()
	{
		if (InactiveGraphNodeTheme == null)
		{
			return;
		}

		foreach (IBTBehaviorNode behaviorNode in BehaviorGraphNodeLookup.EnumerateBehaviorNodes(this))
		{
			behaviorNode.Theme = InactiveGraphNodeTheme;
		}
	}

	private void ApplyActiveHighlight(string rootInstanceId)
	{
		HashSet<string> nextIds = CollectActiveSubtreeInstanceIds(rootInstanceId);
		if (_highlightedInstanceIds.SetEquals(nextIds))
		{
			return;
		}

		ClearActiveHighlight();
		if (nextIds.Count == 0 || ActiveGraphNodeTheme == null)
		{
			return;
		}

		foreach (string instanceId in nextIds)
		{
			_highlightedInstanceIds.Add(instanceId);
			if (BehaviorGraphNodeLookup.GetBehaviorNode(this, instanceId) is IBTBehaviorNode behaviorNode)
			{
				behaviorNode.Theme = ActiveGraphNodeTheme;
			}
		}
	}

	private HashSet<string> CollectActiveSubtreeInstanceIds(string rootInstanceId)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		if (string.IsNullOrEmpty(rootInstanceId) || _workingChart == null)
		{
			return ids;
		}

		CollectDescendantInstanceIds(rootInstanceId, ids);
		return ids;
	}

	private void CollectDescendantInstanceIds(string instanceId, HashSet<string> ids)
	{
		if (!ids.Add(instanceId))
		{
			return;
		}

		if (!_workingChart!.BehaviorNodes.TryGetValue(instanceId, out BehaviorNode? node) || node == null)
		{
			return;
		}

		string[] childIds = node.ChildBehaviorInstanceIds ?? Array.Empty<string>();
		for (int i = 0; i < childIds.Length; i++)
		{
			CollectDescendantInstanceIds(childIds[i], ids);
		}
	}

	private void ClearActiveHighlight()
	{
		if (InactiveGraphNodeTheme == null)
		{
			_highlightedInstanceIds.Clear();
			return;
		}

		foreach (string instanceId in _highlightedInstanceIds)
		{
			if (BehaviorGraphNodeLookup.GetBehaviorNode(this, instanceId) is IBTBehaviorNode behaviorNode)
			{
				behaviorNode.Theme = InactiveGraphNodeTheme;
			}
		}

		_highlightedInstanceIds.Clear();
	}

	private void UpdatePushButtonAvailability()
	{
		PushGraphButton.Disabled = _activeHost == null || _workingChart == null;
	}
}

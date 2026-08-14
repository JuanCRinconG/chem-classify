#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using IBTSystem;

namespace IBTPlugin;

/// <summary>Attach-time options for a shared behavior graph surface.</summary>
public sealed class BehaviorGraphSurfaceOptions
{
	public required PackedScene BehaviorNodeScene { get; init; }
	public required PackedScene TransitionEngineNodeScene { get; init; }
	public Func<IBTData?>? GetBehaviorData { get; init; }
	public Action<IBTChart>? CommitHandler { get; init; }
	public Action<IBTBehaviorNode, int>? OnChildWireTopologyChanged { get; init; }
	public Action? OnChartTopologyChanged { get; init; }
}

/// <summary>Graph-edit surface: wiring, layout, population, and initial-behavior selection.</summary>
public sealed class BehaviorGraphSurface
{
	private readonly GraphEdit _graphEdit;
	private readonly BehaviorGraphChartState _chartState;
	private readonly BehaviorGraphSurfaceOptions _options;
	private readonly IBTBindings _initialBehaviorBindings = new();

	private int _nextFallbackPositionIndex;
	private bool _hasInitialBehaviorButtonGroup;
	private ButtonGroup _initialBehaviorButtonGroup = null!;

	private BehaviorGraphSurface(
		GraphEdit graphEdit,
		BehaviorGraphChartState chartState,
		BehaviorGraphSurfaceOptions options)
	{
		_graphEdit = graphEdit;
		_chartState = chartState;
		_options = options;
	}

	public static BehaviorGraphSurface Attach(
		GraphEdit graphEdit,
		BehaviorGraphChartState chartState,
		BehaviorGraphSurfaceOptions options) =>
		new(graphEdit, chartState, options);

	public void ConfigureConnectionTypes()
	{
		_graphEdit.RightDisconnects = true;
		_graphEdit.AddValidConnectionType(
			(int)IBTGraphConnectionType.ParentChild,
			(int)IBTGraphConnectionType.ParentChild);
		_graphEdit.AddValidConnectionType(
			(int)IBTGraphConnectionType.TransitionOutput,
			(int)IBTGraphConnectionType.TransitionInput);
		_graphEdit.AddValidRightDisconnectType((int)IBTGraphConnectionType.ParentChild);
		_graphEdit.AddValidRightDisconnectType((int)IBTGraphConnectionType.TransitionOutput);
	}

	public void BindInteractionSignals(IBTBindings bindings)
	{
		bindings.BindSignal(
			_graphEdit,
			GraphEdit.SignalName.ConnectionRequest,
			Callable.From((StringName fromName, long fromPort, StringName toName, long toPort) =>
				OnConnectionRequest(fromName, fromPort, toName, toPort)));
		bindings.BindSignal(
			_graphEdit,
			GraphEdit.SignalName.DisconnectionRequest,
			Callable.From((StringName fromName, long fromPort, StringName toName, long toPort) =>
				OnDisconnectionRequest(fromName, fromPort, toName, toPort)));
		bindings.BindSignal(
			_graphEdit,
			GraphEdit.SignalName.EndNodeMove,
			Callable.From(SaveNodePositions));
	}

	public void Teardown()
	{
		_initialBehaviorBindings.Clear();
		_hasInitialBehaviorButtonGroup = false;
		_initialBehaviorButtonGroup = null!;
		_nextFallbackPositionIndex = 0;
		ClearGraphNodes();
	}

	public void LoadChart()
	{
		EnsureInitialBehaviorButtonGroup();
		RebuildGraph();
		RefreshInitialBehaviorButtons();
	}

	public void RefreshFromData()
	{
		IBTChart? chart = _chartState.Chart;
		if (!chart.IsValid())
		{
			return;
		}

		IBTData? data = ResolveBehaviorData();
		foreach (KeyValuePair<string, BehaviorNode> entry in chart!.BehaviorNodes)
		{
			if (BehaviorGraphNodeLookup.GetBehaviorNode(_graphEdit, entry.Key) is IBTBehaviorNode graphNode)
			{
				RefreshGraphNode(entry.Key, graphNode, data);
			}
		}

		foreach (KeyValuePair<string, TransitionEngineNode> entry in chart.TransitionEngineNodes)
		{
			if (BehaviorGraphNodeLookup.GetTransitionEngineNode(_graphEdit, entry.Key) is IBTTransitionNode graphNode)
			{
				RefreshGraphNode(entry.Key, graphNode, data);
			}
		}

		RefreshInitialBehaviorButtons();
	}

	public void CommitChart()
	{
		IBTChart? chart = _chartState.Chart;
		if (chart == null)
		{
			return;
		}

		if (_options.CommitHandler != null)
		{
			_options.CommitHandler(chart);
			return;
		}

		BehaviorChartPersistence.TryPersistToDisk(chart);
	}

	public void SetInitialBehavior(string instanceId)
	{
		IBTChart? chart = _chartState.Chart;
		if (chart == null
			|| string.IsNullOrWhiteSpace(instanceId)
			|| !chart.BehaviorNodes.ContainsKey(instanceId))
		{
			return;
		}

		chart.InitialBehaviorInstanceId = instanceId;
		CommitChart();
	}

	internal int AllocateFallbackPositionIndex() => _nextFallbackPositionIndex++;

	private IBTData? ResolveBehaviorData() => _options.GetBehaviorData?.Invoke();

	private void RebuildGraph()
	{
		_chartState.IsSyncingGraph = true;
		ClearGraphNodes();
		_nextFallbackPositionIndex = 0;

		IBTChart? chart = _chartState.Chart;
		if (chart.IsValid())
		{
			chart!.EnsureCollections();
			IBTData? data = ResolveBehaviorData();
			foreach (KeyValuePair<string, BehaviorNode> entry in chart.BehaviorNodes)
			{
				if (entry.Value is BehaviorNode node)
				{
					InstantiateBehaviorNode(
						entry.Key,
						node,
						BehaviorChartGraphLayout.ResolvePosition(node.Position, _nextFallbackPositionIndex),
						chart,
						data);
					_nextFallbackPositionIndex++;
				}
			}

			foreach (KeyValuePair<string, TransitionEngineNode> entry in chart.TransitionEngineNodes)
			{
				if (entry.Value is TransitionEngineNode node)
				{
					InstantiateTransitionEngineNode(
						entry.Key,
						node,
						BehaviorChartGraphLayout.ResolvePosition(
							node.Position,
							chart.BehaviorNodes.Count + _nextFallbackPositionIndex),
						data);
					_nextFallbackPositionIndex++;
				}
			}

			SyncAllVisualWires();
		}

		_chartState.IsSyncingGraph = false;
	}

	internal void EnsureInitialBehaviorButtonGroup()
	{
		if (_hasInitialBehaviorButtonGroup)
		{
			return;
		}

		_initialBehaviorButtonGroup = new ButtonGroup { AllowUnpress = false };
		_hasInitialBehaviorButtonGroup = true;
		_initialBehaviorBindings.BindSignal(
			_initialBehaviorButtonGroup,
			ButtonGroup.SignalName.Pressed,
			Callable.From((BaseButton button) => OnInitialBehaviorButtonPressed(button)));
	}

	internal void RefreshInitialBehaviorButtons()
	{
		string initialId = _chartState.Chart?.InitialBehaviorInstanceId ?? string.Empty;
		foreach (IBTBehaviorNode behaviorNode in BehaviorGraphNodeLookup.EnumerateBehaviorNodes(_graphEdit))
		{
			behaviorNode.SetAsInitialButton.SetPressedNoSignal(behaviorNode.InstanceId == initialId);
		}
	}

	private void OnInitialBehaviorButtonPressed(BaseButton button)
	{
		string instanceId = button
			.GetMeta(BehaviorGraphNodeLookup.InitialBehaviorInstanceIdMeta, new StringName())
			.AsString();
		if (!string.IsNullOrEmpty(instanceId))
		{
			SetInitialBehavior(instanceId);
		}
	}

	private void SyncAllVisualWires()
	{
		_chartState.IsSyncingGraph = true;
		try
		{
			_graphEdit.ClearConnections();
			foreach (Node child in _graphEdit.GetChildren())
			{
				switch (child)
				{
					case IBTBehaviorNode behaviorSource:
						ReconnectTransitions(behaviorSource);
						ReconnectChildWires(behaviorSource);
						break;
					case IBTTransitionNode transitionSource when transitionSource is not IBTBehaviorNode:
						ReconnectTransitions(transitionSource);
						break;
				}
			}
		}
		finally
		{
			_chartState.IsSyncingGraph = false;
		}
	}

	internal void RefreshGraphNode(string chartKey, IBTTransitionNode graphNode, IBTData? data)
	{
		DisconnectOutgoingTransitionWires(graphNode);
		if (graphNode is IBTBehaviorNode behaviorNode)
		{
			behaviorNode.RefreshFromChart(chartKey, _chartState.Chart, data);
			ReconnectTransitions(behaviorNode);
			ReconnectChildWires(behaviorNode);
		}
		else
		{
			graphNode.RefreshFromChart(chartKey, data);
			ReconnectTransitions(graphNode);
		}
	}

	private void ReconnectTransitions(IBTTransitionNode source)
	{
		if (source.TransitionNode?.Transitions == null)
		{
			return;
		}

		foreach (KeyValuePair<string, string> transition in source.TransitionNode.Transitions)
		{
			if (string.IsNullOrWhiteSpace(transition.Value)
				|| !source.TryFindTransitionOutputPort(transition.Key, out int sourcePort)
				|| BehaviorGraphNodeLookup.GetBehaviorNode(_graphEdit, transition.Value)
					is not IBTBehaviorNode destination)
			{
				continue;
			}

			_graphEdit.ConnectNode(
				source.Name,
				sourcePort,
				destination.Name,
				IBTBehaviorNode.TransitionInputPort);
		}
	}

	private void ReconnectChildWires(IBTBehaviorNode source)
	{
		string[] childIds = source.ChartNode?.ChildBehaviorInstanceIds ?? Array.Empty<string>();
		for (int i = 0; i < childIds.Length; i++)
		{
			if (BehaviorGraphNodeLookup.GetBehaviorNode(_graphEdit, childIds[i]) is IBTBehaviorNode destination)
			{
				_graphEdit.ConnectNode(
					source.Name,
					IBTBehaviorNode.ParentChildPort,
					destination.Name,
					IBTBehaviorNode.ParentChildPort);
			}
		}
	}

	private void DisconnectOutgoingTransitionWires(IBTTransitionNode source)
	{
		StringName nodeName = source.Name;
		DisconnectWhere(connection =>
		{
			if (connection["from_node"].AsStringName() != nodeName)
			{
				return false;
			}

			int fromPort = connection["from_port"].AsInt32();
			return source is not IBTBehaviorNode || fromPort != IBTBehaviorNode.ParentChildPort;
		});
	}

	private void DisconnectWhere(Func<Godot.Collections.Dictionary, bool> predicate)
	{
		Godot.Collections.Array<Godot.Collections.Dictionary> connections = _graphEdit.GetConnectionList();
		for (int i = connections.Count - 1; i >= 0; i--)
		{
			Godot.Collections.Dictionary connection = connections[i];
			if (!predicate(connection))
			{
				continue;
			}

			_graphEdit.DisconnectNode(
				connection["from_node"].AsStringName(),
				connection["from_port"].AsInt32(),
				connection["to_node"].AsStringName(),
				connection["to_port"].AsInt32());
		}
	}

	private void OnConnectionRequest(StringName fromName, long fromPort, StringName toName, long toPort)
	{
		if (_chartState.IsSyncingGraph
			|| !TryGetBehaviorDestination(toName, toPort, out IBTBehaviorNode destination))
		{
			return;
		}

		var fromPath = new NodePath(fromName);
		if (fromPort == IBTBehaviorNode.ParentChildPort
			&& toPort == IBTBehaviorNode.ParentChildPort
			&& _graphEdit.GetNodeOrNull<IBTBehaviorNode>(fromPath) is IBTBehaviorNode behaviorSource)
		{
			ConnectChildPort(behaviorSource, destination);
			return;
		}

		if (toPort != IBTBehaviorNode.TransitionInputPort
			|| _graphEdit.GetNodeOrNull<IBTTransitionNode>(fromPath) is not IBTTransitionNode transitionSource
			|| !transitionSource.TryGetTransitionName(fromPort, out string transitionName))
		{
			return;
		}

		ConnectTransitionPort(transitionSource, transitionName, (int)fromPort, destination);
	}

	private void OnDisconnectionRequest(StringName fromName, long fromPort, StringName toName, long toPort)
	{
		var fromPath = new NodePath(fromName);
		var toPath = new NodePath(toName);
		if (fromPort == IBTBehaviorNode.ParentChildPort
			&& toPort == IBTBehaviorNode.ParentChildPort
			&& _graphEdit.GetNodeOrNull<IBTBehaviorNode>(fromPath) is IBTBehaviorNode behaviorSource
			&& _graphEdit.GetNodeOrNull<IBTBehaviorNode>(toPath) is IBTBehaviorNode childDestination)
		{
			DisconnectChildPort(behaviorSource, childDestination);
			return;
		}

		if (toPort != IBTBehaviorNode.TransitionInputPort
			|| _graphEdit.GetNodeOrNull<IBTTransitionNode>(fromPath) is not IBTTransitionNode transitionSource
			|| _graphEdit.GetNodeOrNull<IBTBehaviorNode>(toPath) is not IBTBehaviorNode transitionDestination
			|| !transitionSource.TryGetTransitionName(fromPort, out string transitionName))
		{
			return;
		}

		DisconnectTransitionPort(
			transitionSource,
			transitionName,
			(int)fromPort,
			transitionDestination);
	}

	private void ConnectChildPort(IBTBehaviorNode source, IBTBehaviorNode destination)
	{
		IBTChart? chart = _chartState.Chart;
		if (chart == null)
		{
			return;
		}

		int previousChildCount = source.ChartNode?.ChildBehaviorInstanceIds?.Length ?? 0;
		if (!chart.TryConnectChild(source.InstanceId, destination.InstanceId))
		{
			return;
		}

		_graphEdit.ConnectNode(
			source.Name,
			IBTBehaviorNode.ParentChildPort,
			destination.Name,
			IBTBehaviorNode.ParentChildPort);
		_options.OnChildWireTopologyChanged?.Invoke(source, previousChildCount);
		CommitChart();
		_options.OnChartTopologyChanged?.Invoke();
	}

	private void DisconnectChildPort(IBTBehaviorNode source, IBTBehaviorNode destination)
	{
		IBTChart? chart = _chartState.Chart;
		if (chart == null)
		{
			return;
		}

		int previousChildCount = source.ChartNode?.ChildBehaviorInstanceIds?.Length ?? 0;
		if (!chart.TryDisconnectChild(source.InstanceId, destination.InstanceId))
		{
			return;
		}

		_graphEdit.DisconnectNode(
			source.Name,
			IBTBehaviorNode.ParentChildPort,
			destination.Name,
			IBTBehaviorNode.ParentChildPort);
		_options.OnChildWireTopologyChanged?.Invoke(source, previousChildCount);
		CommitChart();
		_options.OnChartTopologyChanged?.Invoke();
	}

	private void ConnectTransitionPort(
		IBTTransitionNode source,
		string transitionName,
		int sourcePort,
		IBTBehaviorNode destination)
	{
		DisconnectExistingTransitionWireFromPort(source, sourcePort);
		if (source.TransitionNode?.TryReconnectTransition(transitionName, destination.InstanceId) != true)
		{
			return;
		}

		_graphEdit.ConnectNode(
			source.Name,
			sourcePort,
			destination.Name,
			IBTBehaviorNode.TransitionInputPort);
		CommitChart();
		_options.OnChartTopologyChanged?.Invoke();
	}

	private void DisconnectTransitionPort(
		IBTTransitionNode source,
		string transitionName,
		int sourcePort,
		IBTBehaviorNode destination)
	{
		if (source.TransitionNode?.TryDisconnectTransition(transitionName, destination.InstanceId) != true)
		{
			return;
		}

		_graphEdit.DisconnectNode(
			source.Name,
			sourcePort,
			destination.Name,
			IBTBehaviorNode.TransitionInputPort);
		CommitChart();
		_options.OnChartTopologyChanged?.Invoke();
	}

	private void DisconnectExistingTransitionWireFromPort(IBTTransitionNode source, int sourcePort)
	{
		StringName sourceName = source.Name;
		DisconnectWhere(connection =>
			connection["from_node"].AsStringName() == sourceName
			&& connection["from_port"].AsInt32() == sourcePort
			&& connection["to_port"].AsInt32() == IBTBehaviorNode.TransitionInputPort);
	}

	private void SaveNodePositions()
	{
		if (_chartState.IsSyncingGraph || _chartState.Chart == null)
		{
			return;
		}

		foreach (Node child in _graphEdit.GetChildren())
		{
			switch (child)
			{
				case IBTBehaviorNode { ChartNode: not null } behaviorGraphNode:
					behaviorGraphNode.ChartNode!.Position = behaviorGraphNode.PositionOffset;
					break;
				case IBTTransitionNode { TransitionNode: not null } engineGraphNode
					when engineGraphNode is not IBTBehaviorNode:
					engineGraphNode.TransitionNode!.Position = engineGraphNode.PositionOffset;
					break;
			}
		}

		CommitChart();
	}

	internal void InstantiateBehaviorNode(
		string instanceId,
		BehaviorNode chartNode,
		Vector2 position,
		IBTChart chart,
		IBTData? data)
	{
		IBTBehaviorNode graphNode = InstantiateGraphNode<IBTBehaviorNode>(_options.BehaviorNodeScene, position);
		graphNode.RequestChartMutation = CommitChart;
		EnsureInitialBehaviorButtonGroup();
		graphNode.BindIdentity(instanceId, chartNode);
		graphNode.WireInitialBehaviorControl(_initialBehaviorButtonGroup, instanceId);
		graphNode.RefreshFromChart(instanceId, chart, data);
	}

	internal void InstantiateTransitionEngineNode(
		string instanceId,
		TransitionEngineNode chartNode,
		Vector2 position,
		IBTData? data)
	{
		IBTTransitionNode graphNode =
			InstantiateGraphNode<IBTTransitionNode>(_options.TransitionEngineNodeScene, position);
		graphNode.BindFromChart(instanceId, chartNode);
		graphNode.RefreshFromChart(instanceId, data);
	}

	private T InstantiateGraphNode<T>(PackedScene scene, Vector2 position) where T : GraphNode
	{
		var graphNode = scene.Instantiate<T>();
		graphNode.PositionOffset = position;
		_graphEdit.AddChild(graphNode);
		return graphNode;
	}

	internal void RemoveGraphNode(GraphNode graphNode)
	{
		StringName nodeName = graphNode.Name;
		DisconnectWhere(connection =>
		{
			StringName fromNode = connection["from_node"].AsStringName();
			StringName toNode = connection["to_node"].AsStringName();
			return fromNode == nodeName || toNode == nodeName;
		});
		_graphEdit.RemoveChild(graphNode);
		graphNode.QueueFree();
	}

	private void ClearGraphNodes()
	{
		_graphEdit.ClearConnections();
		var nodes = new List<GraphNode>();
		foreach (Node child in _graphEdit.GetChildren())
		{
			if (child is GraphNode graphNode)
			{
				nodes.Add(graphNode);
			}
		}

		for (int i = 0; i < nodes.Count; i++)
		{
			_graphEdit.RemoveChild(nodes[i]);
			nodes[i].QueueFree();
		}
	}

	private bool TryGetBehaviorDestination(StringName nodeName, long port, out IBTBehaviorNode destination)
	{
		destination = _graphEdit.GetNodeOrNull<IBTBehaviorNode>(new NodePath(nodeName))!;
		return destination != null
			&& (port == IBTBehaviorNode.TransitionInputPort
				|| port == IBTBehaviorNode.ParentChildPort);
	}
}

#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTSystem;

/// <summary>
/// Runtime behavior graph built from an <see cref="IBTChart"/>.
/// Behavior-owned transitions wire at build time from factory instances.
/// Abstract emitter transitions wire at build time as slots; bind emitters via
/// <see cref="SetExternalTransitions"/>.
/// </summary>
public partial class BehaviorEngine
{
	private readonly Dictionary<(string Owner, string Group, string Transition), AbstractTransitionSlot>
		_abstractByOwnerTransition = new();

	private BehaviorCore[] _behaviors = Array.Empty<BehaviorCore>();
	private Dictionary<string, BehaviorCore> _instanceIdToCore = null!;
	private IBTChart? _chart;
	private readonly Dictionary<string, object> _boardsByGroup = new();

	/// <summary>
	/// True after factory construction and behavior-owned wiring finished.
	/// Advisory only — early Connect log a warning but do not throw.
	/// </summary>
	public bool IsBuilt { get; private set; }

	/// <summary>
	/// Builds this engine from chart data (does not mutate the shared Resource).
	/// Abstract edges become unbound <see cref="AbstractTransitionSlot"/>s; behavior-owned edges wire immediately.
	/// </summary>
	public BehaviorEngine Build(IBTChart chart)
	{
		ArgumentNullException.ThrowIfNull(chart);
		CaptureRuntimeGraph(chart);
		return this;
	}

	/// <summary>
	/// Registers board targets by binding group, then applies them to built behaviors
	/// by chart <see cref="BehaviorNode.BoardGroup"/>.
	/// Each entry is <c>(target, group)</c>. Null or whitespace group means default.
	/// </summary>
	public void SetBoards(params (object Target, string? Group)[] entries)
	{
		WarnIfNotBuilt(nameof(SetBoards));
		if (entries == null || entries.Length == 0)
		{
			return;
		}

		for (int i = 0; i < entries.Length; i++)
		{
			(object? target, string? group) = entries[i];
			if (target == null)
			{
				GD.PushWarning(
					$"BehaviorEngine.SetBoards: null target skipped for group "
					+ $"'{ChartBindingGroups.FormatForLog(group)}'.");
				continue;
			}

			RegisterBoardGroup(group, target);
		}

		ApplyRegisteredBoards();
	}

	/// <summary>
	/// Binds chart external-transition slots whose binding group and declaring type match each entry.
	/// Each entry is <c>(target, group)</c>. Null or whitespace group means default.
	/// Replaces prior bindings for the same group and declaring type before wiring.
	/// </summary>
	public void SetExternalTransitions(params (object Target, string? Group)[] entries)
	{
		WarnIfNotBuilt(nameof(SetExternalTransitions));
		if (entries == null || entries.Length == 0)
		{
			return;
		}

		for (int i = 0; i < entries.Length; i++)
		{
			(object? target, string? group) = entries[i];
			if (target == null)
			{
				GD.PushWarning(
					$"BehaviorEngine.SetExternalTransitions: null target skipped for group "
					+ $"'{ChartBindingGroups.FormatForLog(group)}'.");
				continue;
			}

			string resolvedGroup = ChartBindingGroups.Canonical(group);
			UnbindExternalTransitionsFor(resolvedGroup, target);
			BindExternalTransitionsFor(resolvedGroup, target);
		}
	}

	private void WipeBuildState()
	{
		_behaviors = Array.Empty<BehaviorCore>();
		_instanceIdToCore = null!;
		_chart = null;
		_boardsByGroup.Clear();
		_abstractByOwnerTransition.Clear();
		_initialBehavior = null;
		IsBuilt = false;
	}

	private void CaptureRuntimeGraph(IBTChart chart)
	{
		_chart = chart;
		_boardsByGroup.Clear();
		_initialBehavior = null;
		_activeBehavior = null;

		List<(string Owner, string Transition, string Destination, string State)>? behaviorOwnedTransitions = null;
		List<string>? failedSubscriptions = null;
		if (Debug)
		{
			behaviorOwnedTransitions = new List<(string, string, string, string)>();
			failedSubscriptions = new List<string>();
		}

		CreateBehaviorInstances(chart);

		if (!string.IsNullOrWhiteSpace(chart.InitialBehaviorInstanceId)
			&& _instanceIdToCore.TryGetValue(chart.InitialBehaviorInstanceId, out BehaviorCore? initialBehavior))
		{
			_initialBehavior = initialBehavior;
		}

		WireBehaviorHierarchy(chart);
		WireChartTransitions(chart, behaviorOwnedTransitions, failedSubscriptions);

		_instanceIdToCore.Clear();
		IsBuilt = true;

		if (Debug && behaviorOwnedTransitions != null && failedSubscriptions != null)
		{
			LogBuildReport(chart, behaviorOwnedTransitions, failedSubscriptions);
		}
	}

	private void WireChartTransitions(
		IBTChart chart,
		List<(string Owner, string Transition, string Destination, string State)>? behaviorOwnedTransitions,
		List<string>? failedSubscriptions)
	{
		foreach (KeyValuePair<string, BehaviorNode> entry in chart.BehaviorNodes)
		{
			string instanceId = BehaviorDataKeys.Normalize(entry.Key);
			BehaviorNode node = entry.Value;
			if (node.Transitions == null
				|| !_instanceIdToCore.TryGetValue(instanceId, out BehaviorCore? subscribeOwner))
			{
				continue;
			}

			WireBehaviorOwnedTransitions(
				node.FullTypeName,
				node.Transitions,
				subscribeOwner,
				behaviorOwnedTransitions,
				failedSubscriptions);
		}

		foreach (KeyValuePair<string, TransitionEngineNode> entry in chart.TransitionEngineNodes)
		{
			TransitionEngineNode node = entry.Value;
			if (node.Transitions == null || string.IsNullOrWhiteSpace(node.FullTypeName))
			{
				continue;
			}

			WireAbstractTransitionSlots(node.FullTypeName, node.BindingGroup, node.Transitions);
		}
	}

	private void WireBehaviorOwnedTransitions(
		string ownerName,
		Godot.Collections.Dictionary<string, string> transitions,
		BehaviorCore subscribeOwner,
		List<(string Owner, string Transition, string Destination, string State)>? behaviorOwnedTransitions,
		List<string>? failedSubscriptions)
	{
		foreach ((string transitionName, BehaviorCore destination, BehaviorTransitionWire wire) in
			EnumerateResolvedTransitionEdges(ownerName, transitions))
		{
			bool wired = TrySubscribe(
				subscribeOwner,
				wire,
				ownerName,
				transitionName,
				destination,
				failedSubscriptions,
				out _);
			if (behaviorOwnedTransitions == null)
			{
				continue;
			}

			behaviorOwnedTransitions.Add((
				ownerName,
				transitionName,
				destination.GetType().Name,
				wired ? "Wired" : "Failed"));
		}
	}

	private void WireAbstractTransitionSlots(
		string ownerName,
		string? bindingGroup,
		Godot.Collections.Dictionary<string, string> transitions)
	{
		string resolvedGroup = ChartBindingGroups.Canonical(bindingGroup);
		foreach ((string transitionName, BehaviorCore destination, BehaviorTransitionWire wire) in
			EnumerateResolvedTransitionEdges(ownerName, transitions))
		{
			var slotKey = (ownerName, resolvedGroup, transitionName);
			var slot = new AbstractTransitionSlot(
				ownerName,
				resolvedGroup,
				transitionName,
				destination,
				wire);
			if (!_abstractByOwnerTransition.TryAdd(slotKey, slot))
			{
				throw new InvalidOperationException(
					$"BehaviorEngine.Build: duplicate abstract transition "
					+ $"'{ownerName}.{ChartBindingGroups.FormatForLog(resolvedGroup)}.{transitionName}'.");
			}
		}
	}

	private IEnumerable<(string TransitionName, BehaviorCore Destination, BehaviorTransitionWire Wire)>
		EnumerateResolvedTransitionEdges(
			string ownerName,
			Godot.Collections.Dictionary<string, string> transitions)
	{
		foreach (KeyValuePair<string, string> transition in transitions)
		{
			ResolveTransitionEdge(
				ownerName,
				transition.Key,
				transition.Value,
				out BehaviorCore destination,
				out BehaviorTransitionWire wire);
			yield return (transition.Key, destination, wire);
		}
	}

	private void ResolveTransitionEdge(
		string ownerName,
		string transitionName,
		string destinationInstanceId,
		out BehaviorCore destination,
		out BehaviorTransitionWire wire)
	{
		destination = GetRequiredBehavior(destinationInstanceId, ownerName, transitionName);
		wire = IBTManager.GetTransition(ownerName, transitionName);
	}

	private void CreateBehaviorInstances(IBTChart chart)
	{
		_instanceIdToCore = new Dictionary<string, BehaviorCore>(StringComparer.Ordinal);
		var behaviors = new List<BehaviorCore>(chart.BehaviorNodes.Count);

		foreach (KeyValuePair<string, BehaviorNode> entry in chart.BehaviorNodes)
		{
			string instanceId = BehaviorDataKeys.Normalize(entry.Key);
			BehaviorNode node = entry.Value;
			string typeName = node.FullTypeName;
			if (string.IsNullOrWhiteSpace(instanceId))
			{
				throw new InvalidOperationException(
					$"BehaviorEngine.Build: behavior '{typeName}' has an empty instance ID key.");
			}

			BehaviorDataEntry behavior = IBTManager.GetBehavior(typeName);
			object instance = behavior.CreateInstance();
			if (instance is not BehaviorCore core)
			{
				throw new InvalidOperationException(
					$"BehaviorEngine.Build: '{typeName}' must derive from BehaviorCore.");
			}

			core.ChartInstanceId = instanceId;
			if (node.Config != null)
			{
				ApplyConfig(core, behavior, node.Config);
			}

			core.SetProcessOrder(node.ProcessOrder);

			if (!_instanceIdToCore.TryAdd(instanceId, core))
			{
				throw new InvalidOperationException(
					$"BehaviorEngine.Build: duplicate behavior instance ID '{instanceId}'.");
			}

			behaviors.Add(core);
		}

		_behaviors = behaviors.ToArray();
	}

	private void WireBehaviorHierarchy(IBTChart chart)
	{
		var parentByChild = new Dictionary<string, string>(StringComparer.Ordinal);
		var claimedChildren = new HashSet<string>(StringComparer.Ordinal);

		foreach (KeyValuePair<string, BehaviorNode> entry in chart.BehaviorNodes)
		{
			string parentInstanceId = BehaviorDataKeys.Normalize(entry.Key);
			BehaviorNode node = entry.Value;
			if (!_instanceIdToCore.TryGetValue(parentInstanceId, out BehaviorCore? parent))
			{
				throw new InvalidOperationException(
					$"BehaviorEngine.Build: behavior instance '{parentInstanceId}' is missing a runtime core.");
			}

			string[] childInstanceIds = node.ChildBehaviorInstanceIds ?? Array.Empty<string>();

			for (int childOffset = 0; childOffset < childInstanceIds.Length; childOffset++)
			{
				string childInstanceId = childInstanceIds[childOffset];
				if (!_instanceIdToCore.TryGetValue(childInstanceId, out BehaviorCore? child))
				{
					throw new InvalidOperationException(
						$"BehaviorEngine.Build: '{parent.GetType().Name}' references unknown child instance '{childInstanceId}'.");
				}

				if (!claimedChildren.Add(childInstanceId))
				{
					throw new InvalidOperationException(
						$"BehaviorEngine.Build: child instance '{childInstanceId}' has more than one parent.");
				}

				if (WouldCreateHierarchyCycle(parentByChild, parentInstanceId, childInstanceId))
				{
					throw new InvalidOperationException(
						$"BehaviorEngine.Build: child link '{parent.GetType().Name}' -> '{child.GetType().Name}' would create a cycle.");
				}

				parentByChild[childInstanceId] = parentInstanceId;
				parent.Children.Add(child);
			}
		}
	}

	private static bool WouldCreateHierarchyCycle(
		Dictionary<string, string> parentByChild,
		string parentInstanceId,
		string childInstanceId)
	{
		string? current = parentInstanceId;
		while (current != null)
		{
			if (current == childInstanceId)
			{
				return true;
			}

			if (!parentByChild.TryGetValue(current, out current))
			{
				current = null;
			}
		}

		return false;
	}

	private static void ApplyConfig(BehaviorCore core, BehaviorDataEntry entry, Resource config)
	{
		if (entry.ConfigType == null)
		{
			return;
		}

		if (!entry.ConfigType.IsInstanceOfType(config))
		{
			GD.PushWarning(
				$"BehaviorEngine.Build: Config type '{config.GetType().Name}' is not assignable to '{entry.ConfigType.Name}' on '{core.GetType().Name}'.");
			return;
		}

		typeof(BehaviorEngine)
			.GetMethod(nameof(AssignConfig), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
			.MakeGenericMethod(entry.ConfigType)
			.Invoke(null, new object[] { core, config });
	}

	private static void AssignConfig<TConfig>(BehaviorCore core, Resource config)
		where TConfig : Resource =>
		((BehaviorConfig<TConfig>)core).Config = (TConfig)config;

	private void RegisterBoardGroup(string? group, object board) =>
		_boardsByGroup[ChartBindingGroups.Canonical(group)] = board;

	private void ApplyRegisteredBoards()
	{
		if (_chart == null || _boardsByGroup.Count == 0)
		{
			return;
		}

		for (int i = 0; i < _behaviors.Length; i++)
		{
			BehaviorCore core = _behaviors[i];
			BehaviorDataEntry entry = IBTManager.GetBehavior(BehaviorDataKeys.GetBehaviorKey(core.GetType()));
			if (entry.BoardType == null)
			{
				continue;
			}

			string instanceId = BehaviorDataKeys.Normalize(core.ChartInstanceId);
			if (string.IsNullOrEmpty(instanceId)
				|| !_chart.BehaviorNodes.TryGetValue(instanceId, out BehaviorNode? chartNode))
			{
				continue;
			}

			string boardGroup = ChartBindingGroups.Canonical(chartNode.BoardGroup);
			if (!_boardsByGroup.TryGetValue(boardGroup, out object? board))
			{
				continue;
			}

			ApplyBoard(core, entry, board);
		}
	}

	private static void ApplyBoard(BehaviorCore core, BehaviorDataEntry entry, object board)
	{
		if (entry.BoardType == null || !entry.BoardType.IsInstanceOfType(board))
		{
			return;
		}

		typeof(BehaviorEngine)
			.GetMethod(nameof(AssignBoard), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
			.MakeGenericMethod(entry.BoardType)
			.Invoke(null, new object[] { core, board });
	}

	private static void AssignBoard<TBoard>(BehaviorCore core, object board)
		where TBoard : class =>
		((BehaviorBoard<TBoard>)core).Board = (TBoard)board;

	private void UnbindExternalTransitionsFor(string group, object entry)
	{
		foreach (AbstractTransitionSlot slot in _abstractByOwnerTransition.Values)
		{
			if (ChartBindingGroups.GroupsEqual(slot.BindingGroup, group)
				&& slot.Wire.DeclaringType.IsInstanceOfType(entry)
				&& slot.IsBound)
			{
				UnbindSlot(slot);
			}
		}
	}

	private void BindExternalTransitionsFor(string group, object entry)
	{
		bool matchedAnySlot = false;
		foreach (AbstractTransitionSlot slot in _abstractByOwnerTransition.Values)
		{
			if (!ChartBindingGroups.GroupsEqual(slot.BindingGroup, group)
				|| !slot.Wire.DeclaringType.IsInstanceOfType(entry))
			{
				continue;
			}

			matchedAnySlot = true;
			TryBindSlot(slot, entry);
		}

		if (!matchedAnySlot)
		{
			GD.PushWarning(
				$"BehaviorEngine.SetExternalTransitions: no chart slots matched group "
				+ $"'{ChartBindingGroups.FormatForLog(group)}' for '{entry.GetType().Name}'.");
		}
	}

	private void UnbindAllExternalTransitions()
	{
		foreach (AbstractTransitionSlot slot in _abstractByOwnerTransition.Values)
		{
			if (slot.IsBound)
			{
				UnbindSlot(slot);
			}
		}
	}

	private BehaviorCore GetRequiredBehavior(
		string destinationInstanceId,
		string ownerName,
		string transitionName)
	{
		if (_instanceIdToCore.TryGetValue(destinationInstanceId, out BehaviorCore? destination))
		{
			return destination;
		}

		throw new InvalidOperationException(
			$"BehaviorEngine.Build: transition '{ownerName}.{transitionName}' targets unknown behavior instance '{destinationInstanceId}'.");
	}

	private bool TryBindSlot(AbstractTransitionSlot slot, object context)
	{
		if (!TrySubscribe(
				context,
				slot.Wire,
				slot.TypeName,
				slot.TransitionName,
				slot.Destination,
				buildFailures: null,
				out Delegate? handler)
			|| handler == null)
		{
			return false;
		}

		slot.Bind(context, handler);
		LogAbstractTransitionBound(slot.TypeName, slot.BindingGroup, slot.TransitionName);
		return true;
	}

	private void UnbindSlot(AbstractTransitionSlot slot)
	{
		if (!slot.IsBound)
		{
			return;
		}

		try
		{
			slot.Wire.EventInfo.RemoveEventHandler(slot.Context!, slot.Handler!);
		}
		catch (Exception ex)
		{
			GD.PushWarning(
				$"BehaviorEngine.Unbind: failed to unsubscribe '{slot.TransitionName}' on '{slot.Context!.GetType().Name}': {ex.Message}");
		}

		slot.Unbind();
	}

	private bool TrySubscribe(
		object target,
		BehaviorTransitionWire wire,
		string ownerName,
		string transitionName,
		BehaviorCore destination,
		List<string>? buildFailures,
		out Delegate? handler)
	{
		handler = null;

		Action bridge = () => TransitionTo(ownerName, transitionName, destination);
		try
		{
			handler = Delegate.CreateDelegate(wire.HandlerType, bridge.Target, bridge.Method);
			wire.EventInfo.AddEventHandler(target, handler);
			return true;
		}
		catch (Exception ex)
		{
			string message =
				$"failed to subscribe '{ownerName}.{transitionName}' on '{target.GetType().Name}': {ex.Message}";
			GD.PushWarning($"BehaviorEngine: {message}");
			buildFailures?.Add(message);
			handler = null;
			return false;
		}
	}

	private void WarnIfNotBuilt(string apiName)
	{
		if (!IsBuilt)
		{
			GD.PushWarning(
				$"BehaviorEngine.{apiName}: engine is not built yet (IsBuilt == false); continuing anyway.");
		}
	}

	/// <summary>
	/// One abstract chart edge: unbound until host Connect*, then holds context + handler.
	/// </summary>
	private sealed class AbstractTransitionSlot
	{
		public AbstractTransitionSlot(
			string typeName,
			string bindingGroup,
			string transitionName,
			BehaviorCore destination,
			BehaviorTransitionWire wire)
		{
			TypeName = typeName;
			BindingGroup = bindingGroup;
			TransitionName = transitionName;
			Destination = destination;
			Wire = wire;
		}

		public string TypeName { get; }
		public string BindingGroup { get; }
		public string TransitionName { get; }
		public BehaviorCore Destination { get; }
		public BehaviorTransitionWire Wire { get; }
		public object? Context { get; private set; }
		public Delegate? Handler { get; private set; }
		public bool IsBound => Context != null && Handler != null;

		public void Bind(object context, Delegate handler)
		{
			Context = context;
			Handler = handler;
		}

		public void Unbind()
		{
			Context = null;
			Handler = null;
		}
	}
}

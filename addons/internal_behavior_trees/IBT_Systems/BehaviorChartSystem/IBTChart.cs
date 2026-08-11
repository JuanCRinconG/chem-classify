#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTSystem;

/// <summary>
/// Designer orchestration Resource for IBT.
/// Pure data only — no instances, active state, or signal wiring.
/// Runtime is built separately by <see cref="BehaviorEngine"/>.
/// </summary>
[Tool]
[GlobalClass]
public partial class IBTChart : Resource
{
	/// <summary>
	/// Opaque ID of the behavior instance entered when the engine starts. It must be a key in
	/// <see cref="BehaviorNodes"/>.
	/// </summary>
	[Export]
	public string InitialBehaviorInstanceId { get; set; } = string.Empty;

	/// <summary>Transition-engine instance ID → chart node payload.</summary>
	[Export]
	public Godot.Collections.Dictionary<string, TransitionEngineNode> TransitionEngineNodes { get; set; } = new();

	/// <summary>Behavior instance ID → chart node payload.</summary>
	[Export]
	public Godot.Collections.Dictionary<string, BehaviorNode> BehaviorNodes { get; set; } = new();

	public void EnsureCollections()
	{
		TransitionEngineNodes ??= new Godot.Collections.Dictionary<string, TransitionEngineNode>();
		BehaviorNodes ??= new Godot.Collections.Dictionary<string, BehaviorNode>();
		NormalizeBindingGroups();
	}

	private void NormalizeBindingGroups()
	{
		foreach (KeyValuePair<string, BehaviorNode> entry in BehaviorNodes)
		{
			BehaviorNode? node = entry.Value;
			if (node != null)
			{
				node.BoardGroup = ChartBindingGroups.Normalize(node.BoardGroup);
			}
		}

		foreach (KeyValuePair<string, TransitionEngineNode> entry in TransitionEngineNodes)
		{
			TransitionEngineNode? node = entry.Value;
			if (node != null)
			{
				node.BindingGroup = ChartBindingGroups.Normalize(node.BindingGroup);
			}
		}
	}

	/// <summary>Adds a behavior instance when the ID is unique and valid.</summary>
	public bool TryAddBehaviorNode(string instanceId, BehaviorNode node)
	{
		EnsureCollections();
		instanceId = BehaviorDataKeys.Normalize(instanceId);
		if (string.IsNullOrEmpty(instanceId) || node == null)
		{
			return false;
		}

		return BehaviorNodes.TryAdd(instanceId, node);
	}

	/// <summary>Adds a transition-engine instance when the ID and binding group are unique.</summary>
	public bool TryAddTransitionEngineNode(string instanceId, TransitionEngineNode node)
	{
		EnsureCollections();
		instanceId = BehaviorDataKeys.Normalize(instanceId);
		if (string.IsNullOrEmpty(instanceId)
			|| node == null
			|| string.IsNullOrWhiteSpace(node.FullTypeName)
			|| TransitionEngineNodes.ContainsKey(instanceId)
			|| HasTransitionEngineBinding(node.FullTypeName, node.BindingGroup))
		{
			return false;
		}

		node.BindingGroup = ChartBindingGroups.Normalize(node.BindingGroup);
		TransitionEngineNodes[instanceId] = node;
		return true;
	}

	/// <summary>
	/// Applies a data-validated behavior replacement while preserving this chart's
	/// instance topology and incoming transition destinations.
	/// </summary>
	public bool TryChangeBehaviorType(
		string instanceId,
		string typeName,
		string configTypeName,
		IEnumerable<string> availableTransitionNames)
	{
		if (!TryResolveBehaviorNode(ref instanceId, out BehaviorNode behaviorNode)
			|| string.IsNullOrWhiteSpace(typeName))
		{
			return false;
		}

		behaviorNode.ApplyBehaviorTypeChange(typeName, configTypeName, availableTransitionNames);
		return true;
	}

	/// <summary>
	/// Removes behavior instances and records each affected parent's child count before
	/// topology was updated.
	/// </summary>
	public int RemoveBehaviorNodes(
		IReadOnlyList<string> instanceIds,
		Dictionary<string, int> affectedParentPreviousChildCounts)
	{
		EnsureCollections();
		ArgumentNullException.ThrowIfNull(instanceIds);
		ArgumentNullException.ThrowIfNull(affectedParentPreviousChildCounts);

		var normalizedIds = new List<string>();
		for (int i = 0; i < instanceIds.Count; i++)
		{
			string instanceId = BehaviorDataKeys.Normalize(instanceIds[i]);
			if (!string.IsNullOrEmpty(instanceId))
			{
				normalizedIds.Add(instanceId);
			}
		}

		if (normalizedIds.Count == 0)
		{
			return 0;
		}

		for (int i = 0; i < normalizedIds.Count; i++)
		{
			CaptureAffectedParentPreviousChildCounts(normalizedIds[i], affectedParentPreviousChildCounts);
		}

		int removed = 0;
		for (int i = 0; i < normalizedIds.Count; i++)
		{
			if (TryRemoveBehaviorNode(normalizedIds[i]))
			{
				removed++;
			}
		}

		return removed;
	}

	/// <summary>
	/// Removes a behavior instance and all chart-owned topology and transition references
	/// that target it.
	/// </summary>
	public bool TryRemoveBehaviorNode(string instanceId)
	{
		EnsureCollections();
		instanceId = BehaviorDataKeys.Normalize(instanceId);
		if (string.IsNullOrEmpty(instanceId) || !BehaviorNodes.Remove(instanceId))
		{
			return false;
		}

		if (InitialBehaviorInstanceId == instanceId)
		{
			InitialBehaviorInstanceId = string.Empty;
		}

		foreach (KeyValuePair<string, BehaviorNode> entry in BehaviorNodes)
		{
			BehaviorNode? node = entry.Value;
			if (node == null)
			{
				continue;
			}

			var childIds = new List<string>(node.ChildBehaviorInstanceIds ?? Array.Empty<string>());
			if (childIds.RemoveAll(childId => childId == instanceId) > 0)
			{
				node.ChildBehaviorInstanceIds = childIds.ToArray();
			}

			node.RemoveTransitionDestinationsTo(instanceId);
		}

		foreach (KeyValuePair<string, TransitionEngineNode> entry in TransitionEngineNodes)
		{
			entry.Value?.RemoveTransitionDestinationsTo(instanceId);
		}

		return true;
	}

	/// <summary>Removes a transition-engine node from this chart.</summary>
	public bool TryRemoveTransitionEngineNode(string instanceId)
	{
		EnsureCollections();
		instanceId = BehaviorDataKeys.Normalize(instanceId);
		return !string.IsNullOrEmpty(instanceId)
			&& TransitionEngineNodes.Remove(instanceId);
	}

	/// <summary>
	/// Adds a direct child port when topology invariants allow it.
	/// </summary>
	public bool TryConnectChild(string parentInstanceId, string childInstanceId)
	{
		EnsureCollections();
		childInstanceId = BehaviorDataKeys.Normalize(childInstanceId);
		if (!TryResolveBehaviorNode(ref parentInstanceId, out BehaviorNode parentNode)
			|| string.IsNullOrEmpty(childInstanceId)
			|| string.Equals(parentInstanceId, childInstanceId, StringComparison.Ordinal)
			|| HasParent(childInstanceId)
			|| WouldCreateChildCycle(parentInstanceId, childInstanceId))
		{
			return false;
		}

		string[] existingChildren = parentNode.ChildBehaviorInstanceIds ?? Array.Empty<string>();
		if (Array.IndexOf(existingChildren, childInstanceId) >= 0)
		{
			return false;
		}

		var children = new List<string>(existingChildren) { childInstanceId };
		parentNode.ChildBehaviorInstanceIds = children.ToArray();
		return true;
	}

	/// <summary>Removes one direct child port from a parent behavior instance.</summary>
	public bool TryDisconnectChild(string parentInstanceId, string childInstanceId)
	{
		EnsureCollections();
		childInstanceId = BehaviorDataKeys.Normalize(childInstanceId);
		if (!TryResolveBehaviorNode(ref parentInstanceId, out BehaviorNode parentNode)
			|| string.IsNullOrEmpty(childInstanceId))
		{
			return false;
		}

		var children = new List<string>(parentNode.ChildBehaviorInstanceIds ?? Array.Empty<string>());
		if (!children.Remove(childInstanceId))
		{
			return false;
		}

		parentNode.ChildBehaviorInstanceIds = children.ToArray();
		return true;
	}

	/// <summary>
	/// Applies a data-validated transition-engine replacement while preserving instance
	/// identity, binding group, position, and compatible transition wires.
	/// </summary>
	public bool TryChangeTransitionEngineType(
		string instanceId,
		string newEmitterTypeName,
		IEnumerable<string> availableTransitionNames)
	{
		EnsureCollections();
		instanceId = BehaviorDataKeys.Normalize(instanceId);
		newEmitterTypeName = BehaviorDataKeys.Normalize(newEmitterTypeName);
		if (string.IsNullOrEmpty(instanceId)
			|| string.IsNullOrWhiteSpace(newEmitterTypeName)
			|| !TransitionEngineNodes.TryGetValue(instanceId, out TransitionEngineNode? transitionEngineNode)
			|| transitionEngineNode == null)
		{
			return false;
		}

		if (BehaviorDataKeys.KeysEqual(transitionEngineNode.FullTypeName, newEmitterTypeName))
		{
			return true;
		}

		if (HasTransitionEngineBinding(
			newEmitterTypeName,
			transitionEngineNode.BindingGroup,
			exceptInstanceId: instanceId))
		{
			return false;
		}

		transitionEngineNode.ApplyFullTypeChange(newEmitterTypeName, availableTransitionNames);
		return true;
	}

	/// <summary>
	/// Updates a transition-engine binding group when the target pair is unused.
	/// </summary>
	public bool TrySetTransitionEngineBindingGroup(
		string instanceId,
		StringName bindingGroup,
		string? exceptInstanceId = null)
	{
		EnsureCollections();
		instanceId = BehaviorDataKeys.Normalize(instanceId);
		bindingGroup = ChartBindingGroups.Normalize(bindingGroup);
		if (string.IsNullOrEmpty(instanceId)
			|| !TransitionEngineNodes.TryGetValue(instanceId, out TransitionEngineNode? node)
			|| node == null)
		{
			return false;
		}

		if (ChartBindingGroups.GroupsEqual(node.BindingGroup, bindingGroup))
		{
			return true;
		}

		if (HasTransitionEngineBinding(node.FullTypeName, bindingGroup, exceptInstanceId ?? instanceId))
		{
			return false;
		}

		node.BindingGroup = bindingGroup;
		return true;
	}

	/// <summary>Returns whether an emitter type already uses a binding group on this chart.</summary>
	public bool HasTransitionEngineBinding(
		string emitterTypeName,
		StringName bindingGroup,
		string? exceptInstanceId = null)
	{
		EnsureCollections();
		emitterTypeName = BehaviorDataKeys.Normalize(emitterTypeName);
		bindingGroup = ChartBindingGroups.Normalize(bindingGroup);
		exceptInstanceId = BehaviorDataKeys.Normalize(exceptInstanceId);
		if (string.IsNullOrEmpty(emitterTypeName))
		{
			return false;
		}

		foreach (KeyValuePair<string, TransitionEngineNode> entry in TransitionEngineNodes)
		{
			if (!string.IsNullOrEmpty(exceptInstanceId)
				&& BehaviorDataKeys.KeysEqual(entry.Key, exceptInstanceId))
			{
				continue;
			}

			TransitionEngineNode? node = entry.Value;
			if (node == null)
			{
				continue;
			}

			if (BehaviorDataKeys.KeysEqual(node.FullTypeName, emitterTypeName)
				&& ChartBindingGroups.GroupsEqual(node.BindingGroup, bindingGroup))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Renames a shared board binding group across every behavior node on this chart.</summary>
	public bool TryRenameBoardGroup(StringName fromGroup, StringName toGroup)
	{
		EnsureCollections();
		fromGroup = ChartBindingGroups.Normalize(fromGroup);
		toGroup = ChartBindingGroups.Normalize(toGroup);
		if (ChartBindingGroups.GroupsEqual(fromGroup, toGroup))
		{
			return false;
		}

		bool changed = false;
		foreach (KeyValuePair<string, BehaviorNode> entry in BehaviorNodes)
		{
			BehaviorNode? node = entry.Value;
			if (node == null || !ChartBindingGroups.GroupsEqual(node.BoardGroup, fromGroup))
			{
				continue;
			}

			node.BoardGroup = toGroup;
			changed = true;
		}

		return changed;
	}

	private void CaptureAffectedParentPreviousChildCounts(
		string removedChildInstanceId,
		Dictionary<string, int> output)
	{
		foreach (KeyValuePair<string, BehaviorNode> entry in BehaviorNodes)
		{
			BehaviorNode? node = entry.Value;
			if (node == null)
			{
				continue;
			}

			string[] childIds = node.ChildBehaviorInstanceIds ?? Array.Empty<string>();
			if (Array.IndexOf(childIds, removedChildInstanceId) >= 0)
			{
				output[entry.Key] = childIds.Length;
			}
		}
	}

	private bool TryResolveBehaviorNode(ref string instanceId, out BehaviorNode node)
	{
		EnsureCollections();
		instanceId = BehaviorDataKeys.Normalize(instanceId);
		if (string.IsNullOrEmpty(instanceId)
			|| !BehaviorNodes.TryGetValue(instanceId, out BehaviorNode? found)
			|| found == null)
		{
			node = null!;
			return false;
		}

		node = found;
		return true;
	}

	private bool HasParent(string instanceId)
	{
		foreach (KeyValuePair<string, BehaviorNode> entry in BehaviorNodes)
		{
			BehaviorNode? node = entry.Value;
			if (node == null)
			{
				continue;
			}

			if (Array.IndexOf(node.ChildBehaviorInstanceIds ?? Array.Empty<string>(), instanceId) >= 0)
			{
				return true;
			}
		}

		return false;
	}

	private bool WouldCreateChildCycle(string parentInstanceId, string childInstanceId)
	{
		var pending = new Stack<string>();
		var visited = new HashSet<string>(StringComparer.Ordinal);
		pending.Push(childInstanceId);
		while (pending.Count > 0)
		{
			string current = pending.Pop();
			if (!visited.Add(current))
			{
				continue;
			}

			if (current == parentInstanceId)
			{
				return true;
			}

			if (!BehaviorNodes.TryGetValue(current, out BehaviorNode? node))
			{
				continue;
			}

			string[] children = node.ChildBehaviorInstanceIds ?? Array.Empty<string>();
			for (int childIndex = 0; childIndex < children.Length; childIndex++)
			{
				pending.Push(children[childIndex]);
			}
		}

		return false;
	}
}

/// <summary>Runtime chart host contract for wire-debug editing on a live entity.</summary>
public interface EditableBehaviorChart
{
	void GetRuntimeInfo(out IBTChart chart, out BehaviorEngine engine);

	void SetChart(IBTChart chart);
}

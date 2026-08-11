#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTSystem;

/// <summary>
/// Shared chart payload for nodes that own transition destinations and graph-editor pose.
/// Concrete types: <see cref="BehaviorNode"/> and <see cref="TransitionEngineNode"/>.
/// </summary>
[Tool]
public abstract partial class TransitionNode : Resource
{
	/// <summary>
	/// Scan-data type key for this chart row. Behaviors store the qualified behavior key
	/// (<see cref="BehaviorDataKeys.GetBehaviorKey"/>); emitters store the implementing type name.
	/// </summary>
	[Export]
	public string FullTypeName { get; set; } = string.Empty;

	/// <summary>Owned transition-name to destination-instance mappings.</summary>
	[Export]
	public Godot.Collections.Dictionary<string, string> Transitions { get; set; } = new();

	[Export]
	public Vector2 Position { get; set; }

	/// <summary>
	/// Sets or replaces the destination for a transition wire.
	/// </summary>
	public bool TryReconnectTransition(string transitionName, string destinationInstanceId)
	{
		EnsureTransitions();
		if (string.IsNullOrWhiteSpace(transitionName) || string.IsNullOrWhiteSpace(destinationInstanceId))
		{
			return false;
		}

		Transitions[transitionName] = destinationInstanceId;
		return true;
	}

	/// <summary>Disconnects a transition only when it still targets the expected instance.</summary>
	public bool TryDisconnectTransition(string transitionName, string destinationInstanceId)
	{
		EnsureTransitions();
		if (!Transitions.TryGetValue(transitionName, out string? targetId)
			|| targetId != destinationInstanceId)
		{
			return false;
		}

		return Transitions.Remove(transitionName);
	}

	/// <summary>Removes every transition that targets the given behavior instance.</summary>
	public bool RemoveTransitionDestinationsTo(string destinationInstanceId)
	{
		EnsureTransitions();
		var transitionNamesToRemove = new List<string>();
		foreach (KeyValuePair<string, string> transition in Transitions)
		{
			if (transition.Value == destinationInstanceId)
			{
				transitionNamesToRemove.Add(transition.Key);
			}
		}

		for (int i = 0; i < transitionNamesToRemove.Count; i++)
		{
			Transitions.Remove(transitionNamesToRemove[i]);
		}

		return transitionNamesToRemove.Count > 0;
	}

	/// <summary>
	/// Drops persisted transitions whose names are absent from
	/// <paramref name="availableTransitionNames"/> after a data-validated type replacement.
	/// </summary>
	protected void RetainCompatibleTransitions(IEnumerable<string> availableTransitionNames)
	{
		EnsureTransitions();
		var availableNames = new HashSet<string>(availableTransitionNames, StringComparer.Ordinal);
		var transitionNamesToRemove = new List<string>();
		foreach (string transitionName in Transitions.Keys)
		{
			if (!availableNames.Contains(transitionName))
			{
				transitionNamesToRemove.Add(transitionName);
			}
		}

		for (int i = 0; i < transitionNamesToRemove.Count; i++)
		{
			Transitions.Remove(transitionNamesToRemove[i]);
		}
	}

	/// <summary>
	/// Applies a data-validated type replacement while retaining compatible transition wires.
	/// </summary>
	internal void ApplyFullTypeChange(string fullTypeName, IEnumerable<string> availableTransitionNames)
	{
		if (string.Equals(FullTypeName, fullTypeName, StringComparison.Ordinal))
		{
			return;
		}

		RetainCompatibleTransitions(availableTransitionNames);
		FullTypeName = fullTypeName;
	}

	private void EnsureTransitions()
	{
		Transitions ??= new Godot.Collections.Dictionary<string, string>();
	}
}

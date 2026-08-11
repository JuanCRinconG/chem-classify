#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTPlugin;

public enum IBTGraphConnectionType
{
	ParentChild = 0,
	TransitionInput = 1,
	TransitionOutput = 2,
}

/// <summary>Mutable chart + sync flag for a graph surface.</summary>
public sealed class BehaviorGraphChartState
{
	public IBTChart? Chart { get; set; }
	public bool IsSyncingGraph { get; set; }
}

/// <summary>Transition output names projected from scan data plus persisted chart wires.</summary>
public static class BehaviorChartTransitionNames
{
	public static string[] ForBehavior(IBTData? data, BehaviorNode node) =>
		GetRenderedTransitionNames(
			data?.GetTransitionsFromBehavior(node.FullTypeName) ?? Array.Empty<string>(),
			node.Transitions);

	public static string[] ForEmitter(IBTData? data, TransitionEngineNode node) =>
		GetRenderedTransitionNames(
			data?.GetTransitionsFromEmitter(node.FullTypeName) ?? Array.Empty<string>(),
			node.Transitions);

	public static string[] GetRenderedTransitionNames(
		IEnumerable<string> dataTransitionNames,
		Godot.Collections.Dictionary<string, string> persistedWires)
	{
		var names = new HashSet<string>(StringComparer.Ordinal);
		foreach (string name in dataTransitionNames)
		{
			if (!string.IsNullOrWhiteSpace(name))
			{
				names.Add(name);
			}
		}

		if (persistedWires != null)
		{
			foreach (string key in persistedWires.Keys)
			{
				if (!string.IsNullOrWhiteSpace(key))
				{
					names.Add(key);
				}
			}
		}

		var sorted = new string[names.Count];
		names.CopyTo(sorted);
		Array.Sort(sorted, StringComparer.Ordinal);
		return sorted;
	}
}

/// <summary>Stable GraphEdit node names for chart instance IDs and emitter owners.</summary>
public static class BehaviorChartGraphNaming
{
	public static StringName BehaviorNode(string instanceId) => new($"behavior_{instanceId}");

	public static StringName TransitionEngineNode(string instanceId) =>
		new($"transition_engine_{instanceId}");
}

/// <summary>Fallback layout when chart nodes have no saved position.</summary>
public static class BehaviorChartGraphLayout
{
	public static Vector2 ResolvePosition(Vector2 savedPosition, int fallbackIndex) =>
		savedPosition == Vector2.Zero ? FallbackPosition(fallbackIndex) : savedPosition;

	public static Vector2 FallbackPosition(int index) =>
		new(
			40f + index % 4 * 260f,
			40f + index / 4 * 170f);
}

/// <summary>Optional disk persistence for authored charts.</summary>
public static class BehaviorChartPersistence
{
	public static bool TryPersistToDisk(IBTChart chart)
	{
		if (chart == null || string.IsNullOrEmpty(chart.ResourcePath))
		{
			return false;
		}

		chart.EmitChanged();
		return ResourceSaver.Save(chart, chart.ResourcePath) == Error.Ok;
	}
}

/// <summary>Resolves live graph nodes by stable chart naming.</summary>
public static class BehaviorGraphNodeLookup
{
	public static readonly StringName InitialBehaviorInstanceIdMeta = "initial_behavior_instance_id";

	public static IBTBehaviorNode? GetBehaviorNode(GraphEdit graphEdit, string instanceId) =>
		graphEdit.GetNodeOrNull<IBTBehaviorNode>(
			new NodePath(BehaviorChartGraphNaming.BehaviorNode(instanceId)));

	public static IBTTransitionNode? GetTransitionEngineNode(GraphEdit graphEdit, string instanceId) =>
		graphEdit.GetNodeOrNull<IBTTransitionNode>(
			new NodePath(BehaviorChartGraphNaming.TransitionEngineNode(instanceId)));

	public static IEnumerable<IBTBehaviorNode> EnumerateBehaviorNodes(GraphEdit graphEdit)
	{
		foreach (Node child in graphEdit.GetChildren())
		{
			if (child is IBTBehaviorNode behaviorNode)
			{
				yield return behaviorNode;
			}
		}
	}
}

public readonly struct ScanGateLabels
{
	public required string NoDataTitle { get; init; }
	public required string NoDataHint { get; init; }
	public required string DirtyTitle { get; init; }
	public required string DirtyHint { get; init; }
}

public static class BehaviorScanGatePresenter
{
	public static void Apply(
		Control failureSpace,
		Label failureTitle,
		Label failureHint,
		ItemList issuesList,
		Control authoringWorkspace,
		IBTData? data,
		ScanGateLabels labels)
	{
		bool hasData = data.IsValid();
		bool hasIssues = hasData && data!.HasScanIssues;

		failureSpace.Visible = !hasData || hasIssues;
		authoringWorkspace.Visible = hasData && !hasIssues;
		issuesList.Clear();

		if (!hasData)
		{
			failureTitle.Text = labels.NoDataTitle;
			failureHint.Text = labels.NoDataHint;
		}
		else if (hasIssues)
		{
			failureTitle.Text = labels.DirtyTitle;
			failureHint.Text = labels.DirtyHint;
			foreach (string issue in data!.Issues)
			{
				issuesList.AddItem(issue);
			}
		}
	}
}

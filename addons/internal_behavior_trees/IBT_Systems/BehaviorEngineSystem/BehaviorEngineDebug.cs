#nullable enable
using System.Collections.Generic;
using System.Text;
using Godot;

namespace IBTSystem;

public partial class BehaviorEngine
{
	public bool Debug = false;

	private void LogTransitionApplied(string message)
	{
		if (!Debug)
		{
			return;
		}

		GD.Print($"BehaviorEngine transition: {message}");
	}

	private void LogSignalReceived(string ownerName, string transitionName)
	{
		if (!Debug)
		{
			return;
		}

		GD.Print($"BehaviorEngine: received signal {ownerName}.{transitionName}");
	}

	private void LogAbstractTransitionBound(string ownerName, string bindingGroup, string transitionName)
	{
		if (!Debug)
		{
			return;
		}

		GD.Print(
			$"BehaviorEngine: bound abstract transition "
			+ $"{ownerName}.{ChartBindingGroups.FormatForLog(bindingGroup)}.{transitionName}");
	}

	private void LogBuildReport(
		IBTChart chart,
		List<(string Owner, string Transition, string Destination, string State)> behaviorOwnedTransitions,
		List<string> failedSubscriptions)
	{
		if (!Debug)
		{
			return;
		}

		string initialBehaviorNote;
		if (string.IsNullOrWhiteSpace(chart.InitialBehaviorInstanceId))
		{
			initialBehaviorNote = "missing (InitialBehaviorInstanceId is empty)";
		}
		else if (_initialBehavior == null)
		{
			initialBehaviorNote =
				$"missing (instance '{chart.InitialBehaviorInstanceId}' not found on chart)";
		}
		else
		{
			initialBehaviorNote = _initialBehavior.GetType().Name;
		}

		var report = new StringBuilder();
		report.AppendLine("BehaviorEngine build report:");
		report.AppendLine($"  Behavior instances: {_behaviors.Length}");
		report.AppendLine($"  Initial behavior: {initialBehaviorNote}");
		AppendTransitionLines(report, "Behavior-owned transitions", behaviorOwnedTransitions);
		AppendAbstractTransitionLines(report);
		AppendFailedSubscriptions(report, failedSubscriptions);
		GD.Print(report.ToString().TrimEnd());
	}

	private static void AppendTransitionLines(
		StringBuilder report,
		string title,
		IReadOnlyList<(string Owner, string Transition, string Destination, string State)> transitions)
	{
		report.AppendLine($"  {title}: {transitions.Count}");
		for (int i = 0; i < transitions.Count; i++)
		{
			(string owner, string transition, string destination, string state) = transitions[i];
			report.AppendLine($"    {owner}.{transition} -> {destination} [{state}]");
		}
	}

	private void AppendAbstractTransitionLines(StringBuilder report)
	{
		report.AppendLine($"  Abstract transition slots: {_abstractByOwnerTransition.Count}");
		foreach (AbstractTransitionSlot slot in _abstractByOwnerTransition.Values)
		{
			report.AppendLine(
				$"    {slot.TypeName}.{ChartBindingGroups.FormatForLog(slot.BindingGroup)}.{slot.TransitionName}"
				+ $" -> {slot.Destination.GetType().Name} [PendingConnect]");
		}
	}

	private static void AppendFailedSubscriptions(StringBuilder report, IReadOnlyList<string> failedSubscriptions)
	{
		if (failedSubscriptions.Count == 0)
		{
			report.AppendLine("  Failed subscriptions: none");
			return;
		}

		report.AppendLine($"  Failed subscriptions: {failedSubscriptions.Count}");
		for (int i = 0; i < failedSubscriptions.Count; i++)
		{
			report.AppendLine($"    {failedSubscriptions[i]}");
		}
	}
}

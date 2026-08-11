#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace IBTSystem;

/// <summary>
/// Attribute scan and runtime row types for <see cref="IBTData"/>.
/// </summary>
internal static class IBTAttributeScanner
{
	internal static void RunScan(IBTData data)
	{
		var context = new ScanContext();
		CollectAttributes(context);
		EnforceUniqueOwnerTransitionPairs(context);
		ClassifyTransitions(context);
		data.WriteScanToData(context);
	}

	private static void CollectAttributes(ScanContext context)
	{
		Assembly assembly = typeof(BehaviorAttribute).Assembly;
		const BindingFlags EventFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		foreach (Type type in assembly.GetTypes())
		{
			if (type.IsAbstract || type.IsInterface)
			{
				continue;
			}

			string declaringTypeName = GetTypeLabel(type);

			BehaviorAttribute? behaviorAttr = type.GetCustomAttribute<BehaviorAttribute>(inherit: false);
			if (behaviorAttr != null)
			{
				string behaviorName = BehaviorDataKeys.GetBehaviorKey(type);
				string groupName = BehaviorDataKeys.GetGroupName(type);
				if (string.IsNullOrEmpty(behaviorName))
				{
					context.AddIssue(
						"BehaviorKeyInvalid",
						declaringTypeName,
						$"Behavior on type '{declaringTypeName}' has an empty behavior key.");
				}

				if (!IsBehaviorCore(type))
				{
					context.AddIssue(
						"BehaviorBaseTypeInvalid",
						declaringTypeName,
						$"Behavior '{behaviorName}' on type '{declaringTypeName}' must derive from BehaviorCore.");
				}

				Type? configType = FindUniqueGenericArgument(
					type,
					typeof(BehaviorConfig<>),
					context,
					declaringTypeName,
					behaviorName,
					"BehaviorConfigs<>");
				Type? hostType = FindUniqueGenericArgument(
					type,
					typeof(BehaviorBoard<>),
					context,
					declaringTypeName,
					behaviorName,
					"BehaviorHost<>");

				context.Behaviors.Add(new BehaviorDataEntry(
					groupName,
					behaviorName,
					declaringTypeName,
					type,
					configType,
					hostType));
			}

			foreach (EventInfo eventInfo in type.GetEvents(EventFlags))
			{
				BehaviorTransitionAttribute? transitionAttr =
					eventInfo.GetCustomAttribute<BehaviorTransitionAttribute>(inherit: false);
				if (transitionAttr == null)
				{
					continue;
				}

				string transitionName = eventInfo.Name;
				string ownerName = type.GetCustomAttribute<BehaviorAttribute>(inherit: false) != null
					? BehaviorDataKeys.GetBehaviorKey(type)
					: type.Name;

				BehaviorTransitionWire? wire = null;
				if (!TryCreateTransitionWire(eventInfo, type, out wire, out string? wireIssue))
				{
					context.AddIssue("TransitionEventInvalid", declaringTypeName, wireIssue!);
				}

				context.Transitions.Add(new TransitionDataEntry(
					transitionName,
					ownerName,
					declaringTypeName,
					eventInfo.Name,
					IsBehaviorOwned: false,
					BehaviorDataKeys.GetGroupName(type),
					wire));
			}
		}
	}

	private static void EnforceUniqueOwnerTransitionPairs(ScanContext context)
	{
		var seen = new HashSet<(string Owner, string Transition)>();
		var reported = new HashSet<(string Owner, string Transition)>();
		for (int i = 0; i < context.Transitions.Count; i++)
		{
			TransitionDataEntry entry = context.Transitions[i];
			var key = (entry.OwnerName, entry.TransitionName);
			if (!seen.Add(key) && reported.Add(key))
			{
				context.AddIssue(
					"DuplicateOwnerTransition",
					entry.DeclaringTypeName,
					$"Duplicate transition '{entry.TransitionName}' on owner '{entry.OwnerName}'.");
			}
		}
	}

	private static void ClassifyTransitions(ScanContext context)
	{
		if (context.Issues.Count > 0)
		{
			return;
		}

		var behaviorNames = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < context.Behaviors.Count; i++)
		{
			BehaviorDataEntry entry = context.Behaviors[i];
			behaviorNames.Add(entry.BehaviorName);
			context.Behaviors[i] = entry with
			{
				GroupName = BehaviorDataKeys.Normalize(entry.GroupName),
			};
		}

		for (int i = 0; i < context.Transitions.Count; i++)
		{
			TransitionDataEntry entry = context.Transitions[i];
			string ownerName = BehaviorDataKeys.Normalize(entry.OwnerName);
			context.Transitions[i] = entry with
			{
				OwnerName = ownerName,
				GroupName = BehaviorDataKeys.Normalize(entry.GroupName),
				IsBehaviorOwned = behaviorNames.Contains(ownerName),
			};
		}
	}

	private static bool TryCreateTransitionWire(
		EventInfo eventInfo,
		Type declaringType,
		out BehaviorTransitionWire? wire,
		out string? issue)
	{
		wire = null;
		issue = null;

		Type? handlerType = eventInfo.EventHandlerType;
		if (handlerType == null)
		{
			issue = $"Transition '{eventInfo.Name}' on '{GetTypeLabel(declaringType)}' has no EventHandlerType.";
			return false;
		}

		MethodInfo? invoke = handlerType.GetMethod("Invoke");
		ParameterInfo[]? parameters = invoke?.GetParameters();
		if (invoke == null || parameters == null)
		{
			issue = $"Transition '{eventInfo.Name}' on '{GetTypeLabel(declaringType)}' handler type has no Invoke.";
			return false;
		}

		if (parameters.Length != 0)
		{
			issue =
				$"Transition '{eventInfo.Name}' on '{GetTypeLabel(declaringType)}' is not zero-arg (got {parameters.Length} parameters).";
			return false;
		}

		wire = new BehaviorTransitionWire(eventInfo, declaringType, handlerType);
		return true;
	}

	private static bool IsBehaviorCore(Type implType) =>
		typeof(BehaviorCore).IsAssignableFrom(implType);

	private static Type? FindUniqueGenericArgument(
		Type implType,
		Type openGenericInterface,
		ScanContext context,
		string declaringTypeName,
		string behaviorName,
		string capabilityLabel)
	{
		Type? found = null;
		foreach (Type iface in implType.GetInterfaces())
		{
			if (!iface.IsGenericType
				|| iface.GetGenericTypeDefinition() != openGenericInterface)
			{
				continue;
			}

			Type candidate = iface.GetGenericArguments()[0];
			if (found != null && found != candidate)
			{
				context.AddIssue(
					"BehaviorBaseTypeInvalid",
					declaringTypeName,
					$"Behavior '{behaviorName}' on type '{declaringTypeName}' implements multiple {capabilityLabel} contracts ('{found.Name}' and '{candidate.Name}').");
				return null;
			}

			found = candidate;
		}

		return found;
	}

	private static string FormatIssue(string kind, string declaringTypeName, string detail)
		=> $"[{kind}] {declaringTypeName}: {detail}";

	private static string GetTypeLabel(Type type) => type.FullName ?? type.Name;

	internal sealed class ScanContext
	{
		public List<string> Issues { get; } = new();
		public List<BehaviorDataEntry> Behaviors { get; } = new();
		public List<TransitionDataEntry> Transitions { get; } = new();

		public void AddIssue(string kind, string declaringTypeName, string detail)
		{
			Issues.Add(FormatIssue(kind, declaringTypeName, detail));
		}
	}
}

public partial class IBTData
{
	internal void WriteScanToData(IBTAttributeScanner.ScanContext context)
	{
		Clear();
		SetIssues(context.Issues);

		if (context.Issues.Count > 0)
		{
			return;
		}

		var transitionNamesByBehavior = context.Transitions
			.Where(entry => entry.IsBehaviorOwned)
			.ToLookup(entry => entry.OwnerName, entry => entry.TransitionName, StringComparer.Ordinal);
		var transitionNamesByEmitter = context.Transitions
			.Where(entry => !entry.IsBehaviorOwned)
			.ToLookup(entry => entry.OwnerName, entry => entry.TransitionName, StringComparer.Ordinal);

		for (int i = 0; i < context.Behaviors.Count; i++)
		{
			BehaviorDataEntry entry = context.Behaviors[i];
			BehaviorTransitions[entry.BehaviorName] = transitionNamesByBehavior.Contains(entry.BehaviorName)
				? BehaviorDataKeys.ToStringArray(transitionNamesByBehavior[entry.BehaviorName])
				: BehaviorDataKeys.ToStringArray(Array.Empty<string>());
			IBTData.WriteCapabilityTypeName(
				BehaviorConfigTypes,
				entry.BehaviorName,
				entry.ConfigType);
			IBTData.WriteCapabilityTypeName(
				BehaviorBoardTypes,
				entry.BehaviorName,
				entry.BoardType);
		}

		foreach (IGrouping<string, BehaviorDataEntry> group in
			context.Behaviors.GroupBy(entry => entry.GroupName, StringComparer.Ordinal))
		{
			Groups[group.Key] = BehaviorDataKeys.ToStringArray(
				group.Select(entry => entry.BehaviorName));
		}

		foreach (IGrouping<string, string> group in transitionNamesByEmitter)
		{
			EmitterTransitions[group.Key] =
				BehaviorDataKeys.ToStringArray(group);
		}

		foreach (IGrouping<string, TransitionDataEntry> group in context.Transitions
			.Where(entry => !entry.IsBehaviorOwned)
			.GroupBy(entry => entry.GroupName, StringComparer.Ordinal))
		{
			EmitterGroups[group.Key] = BehaviorDataKeys.ToStringArray(
				group.Select(entry => entry.OwnerName).Distinct(StringComparer.Ordinal));
		}

		for (int i = 0; i < context.Behaviors.Count; i++)
		{
			SetRuntimeBehavior(context.Behaviors[i]);
		}

		for (int i = 0; i < context.Transitions.Count; i++)
		{
			SetRuntimeTransition(context.Transitions[i]);
		}
	}
}

/// <summary>
/// Data row for a <see cref="BehaviorAttribute"/> type (soft discovery + factory).
/// <see cref="BehaviorName"/> is the qualified behavior key from <see cref="BehaviorDataKeys.GetBehaviorKey"/>.
/// </summary>
public sealed record BehaviorDataEntry(
	string GroupName,
	string BehaviorName,
	string DeclaringTypeName,
	Type ImplType,
	Type? ConfigType,
	Type? BoardType)
{
	private Func<object>? _factory;

	internal void EnsureFactory()
	{
		_factory ??= () => Activator.CreateInstance(ImplType)
			?? throw new InvalidOperationException(
				$"IBTManager: failed to create '{ImplType.Name}'.");
	}

	public object CreateInstance()
	{
		return _factory?.Invoke()
			?? throw new InvalidOperationException(
				$"Behavior '{BehaviorName}' must be retrieved through IBTManager.GetBehavior before construction.");
	}
}

/// <summary>
/// Data row for a <see cref="BehaviorTransitionAttribute"/> event.
/// </summary>
public sealed record TransitionDataEntry(
	string TransitionName,
	string OwnerName,
	string DeclaringTypeName,
	string EventName,
	bool IsBehaviorOwned,
	string GroupName,
	BehaviorTransitionWire? Wire);

/// <summary>
/// Runtime wire for a data transition event (resolved once at attribute scan).
/// </summary>
public sealed class BehaviorTransitionWire
{
	public BehaviorTransitionWire(EventInfo eventInfo, Type declaringType, Type handlerType)
	{
		EventInfo = eventInfo;
		DeclaringType = declaringType;
		HandlerType = handlerType;
	}

	public EventInfo EventInfo { get; }
	public Type DeclaringType { get; }
	public Type HandlerType { get; }
}

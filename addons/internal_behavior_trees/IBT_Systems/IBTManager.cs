#nullable enable
using System;
using Godot;

namespace IBTSystem;

/// <summary>
/// Static attribute data session for IBT.
/// Soft discovery always returns names/relationships (never throws). Scan diagnostics go through
/// <see cref="IBTAttributeScanner"/> only; runtime factories hard-fail via
/// <see cref="EnsureDataCleanForRuntime"/> when the behavior data is dirty.
/// </summary>
public static class IBTManager
{
	private static IBTData _data = new();
	private static bool _dataScanned;

	/// <summary>
	/// Hard runtime getter and factory binder for a behavior.
	/// Throws when the attribute data is dirty or the behavior is unknown.
	/// </summary>
	public static BehaviorDataEntry GetBehavior(string behaviorName)
	{
		ArgumentNullException.ThrowIfNull(behaviorName);
		EnsureDataCleanForRuntime(nameof(GetBehavior));

		IBTData data = GetData();
		if (data.TryGetRuntimeBehavior(behaviorName, out BehaviorDataEntry found))
		{
			found.EnsureFactory();
			return found;
		}

		throw new InvalidOperationException(
			$"IBTManager.GetBehavior: unknown behavior '{behaviorName}'.");
	}

	/// <summary>
	/// Hard runtime getter for transition wires (behavior-owned and abstract).
	/// Throws when the attribute data is dirty (any scan issue exists).
	/// </summary>
	public static BehaviorTransitionWire GetTransition(
		string ownerName,
		string transitionName)
	{
		ArgumentNullException.ThrowIfNull(ownerName);
		ArgumentNullException.ThrowIfNull(transitionName);
		EnsureDataCleanForRuntime(nameof(GetTransition));

		if (GetData().TryGetRuntimeTransition(ownerName, transitionName, out BehaviorTransitionWire wire))
		{
			return wire;
		}

		throw new InvalidOperationException(
			$"IBTManager.GetTransition: missing transition wire '{ownerName}.{transitionName}'.");
	}

	/// <summary>
	/// Re-scans attributes, then writes into <paramref name="data"/>:
	/// issues first; when any issue exists, group data is left empty.
	/// </summary>
	public static void RefreshData(IBTData data)
	{
		ArgumentNullException.ThrowIfNull(data);

		_data = data;
		_dataScanned = false;
		GetData();
	}

	private static IBTData GetData()
	{
		if (!_dataScanned)
		{
			IBTAttributeScanner.RunScan(_data);
			_dataScanned = true;
		}

		return _data;
	}

	public static IBTData GetRuntimeData()
	{
		return _data;
	}

	/// <summary>
	/// Hard runtime gate — throws when the attribute data is dirty.
	/// Soft callers must not use this; factories are the primary choke point.
	/// </summary>
	private static void EnsureDataCleanForRuntime(string caller)
	{
		Godot.Collections.Array<string> issues = GetData().Issues;
		if (issues.Count == 0)
		{
			return;
		}

		throw new InvalidOperationException(
			$"IBTManager.{caller}: invalid behavior attributes present at runtime. "
			+ "Fix attribute scan issues before playing. "
			+ $"Issues: {string.Join("; ", issues)}");
	}
}

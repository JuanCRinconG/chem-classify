#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTSystem;

/// <summary>
/// Canonical result of an IBT attribute scan. Exported fields provide
/// the string-only projection for soft queries, plugin authoring, and Data Path Maker;
/// non-exported fields retain the runtime reflection bindings for the same scan.
/// </summary>
[Tool]
[GlobalClass]
public partial class IBTData : Resource
{
	private readonly Dictionary<string, BehaviorDataEntry> _runtimeBehaviors =
		new(System.StringComparer.Ordinal);
	private readonly Dictionary<(string Owner, string Transition), BehaviorTransitionWire>
		_runtimeTransitions = new();

	/// <summary>Last projected scan issue lines (empty when clean).</summary>
	[Export]
	public Godot.Collections.Array<string> Issues { get; set; } = new();

	/// <summary>Product group → qualified behavior keys.</summary>
	[Export]
	public Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> Groups { get; set; } = new();

	/// <summary>Qualified behavior key → owned transition names.</summary>
	[Export]
	public Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> BehaviorTransitions { get; set; } = new();

	/// <summary>Emitter type name → owned transition names.</summary>
	[Export]
	public Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> EmitterTransitions { get; set; } = new();

	/// <summary>Product group → emitter implementing type names.</summary>
	[Export]
	public Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> EmitterGroups { get; set; } = new();

	/// <summary>Behavior impl name → config Resource class name (sparse).</summary>
	[Export]
	public Godot.Collections.Dictionary<string, string> BehaviorConfigTypes { get; set; } = new();

	/// <summary>Behavior impl name → board class name (sparse).</summary>
	[Export]
	public Godot.Collections.Dictionary<string, string> BehaviorBoardTypes { get; set; } = new();

	internal void EnsureCollections()
	{
		Issues ??= new Godot.Collections.Array<string>();
		Groups ??= new Godot.Collections.Dictionary<string, Godot.Collections.Array<string>>();
		BehaviorTransitions ??= new Godot.Collections.Dictionary<string, Godot.Collections.Array<string>>();
		EmitterTransitions ??= new Godot.Collections.Dictionary<string, Godot.Collections.Array<string>>();
		EmitterGroups ??= new Godot.Collections.Dictionary<string, Godot.Collections.Array<string>>();
		BehaviorConfigTypes ??= new Godot.Collections.Dictionary<string, string>();
		BehaviorBoardTypes ??= new Godot.Collections.Dictionary<string, string>();
	}

	internal void Clear()
	{
		EnsureCollections();
		Issues.Clear();
		Groups.Clear();
		BehaviorTransitions.Clear();
		EmitterTransitions.Clear();
		EmitterGroups.Clear();
		BehaviorConfigTypes.Clear();
		BehaviorBoardTypes.Clear();
		_runtimeBehaviors.Clear();
		_runtimeTransitions.Clear();
	}

	public bool HasScanIssues
	{
		get
		{
			EnsureCollections();
			return Issues.Count > 0;
		}
	}

	internal void SetIssues(IEnumerable<string> issues)
	{
		EnsureCollections();
		Issues.Clear();
		if (issues == null)
		{
			return;
		}

		foreach (string issue in issues)
		{
			if (!string.IsNullOrEmpty(issue))
			{
				Issues.Add(issue);
			}
		}
	}

	internal void SetRuntimeBehavior(BehaviorDataEntry entry)
	{
		_runtimeBehaviors[entry.BehaviorName] = entry;
	}

	internal void SetRuntimeTransition(TransitionDataEntry entry)
	{
		if (entry.Wire != null)
		{
			_runtimeTransitions[(entry.OwnerName, entry.TransitionName)] = entry.Wire;
		}
	}

	internal bool TryGetRuntimeBehavior(string behaviorName, out BehaviorDataEntry entry) =>
		_runtimeBehaviors.TryGetValue(behaviorName, out entry!);

	internal bool TryGetRuntimeTransition(
		string ownerName,
		string transitionName,
		out BehaviorTransitionWire wire) =>
		_runtimeTransitions.TryGetValue((ownerName, transitionName), out wire!);

	/// <summary>Returns the behavior keys owned by <paramref name="groupName"/>.</summary>
	public string[] GetBehaviorsFromGroup(string groupName) => GetKeysFromGroup(Groups, groupName);

	/// <summary>Returns the emitter type names owned by <paramref name="groupName"/>.</summary>
	public string[] GetEmittersFromGroup(string groupName) => GetKeysFromGroup(EmitterGroups, groupName);

	/// <summary>
	/// Finds the scan namespace group that contains <paramref name="ownerKey"/>
	/// (qualified behavior key or emitter type name).
	/// </summary>
	public bool TryFindGroupForOwner(string ownerKey, out string groupName)
	{
		EnsureCollections();
		string normalizedOwnerKey = BehaviorDataKeys.Normalize(ownerKey);
		if (string.IsNullOrEmpty(normalizedOwnerKey))
		{
			groupName = string.Empty;
			return false;
		}

		if (TryFindOwnerInGroups(Groups, normalizedOwnerKey, out groupName)
			|| TryFindOwnerInGroups(EmitterGroups, normalizedOwnerKey, out groupName))
		{
			return true;
		}

		groupName = string.Empty;
		return false;
	}

	/// <summary>Returns transition names owned by an emitter implementing type.</summary>
	public string[] GetTransitionsFromEmitter(string emitterName) =>
		GetTransitionNames(EmitterTransitions, emitterName);

	/// <summary>Returns all scanned emitter implementing type names.</summary>
	public string[] GetEmitterNames()
	{
		EnsureCollections();
		return BehaviorDataKeys.ListSortedKeys(EmitterTransitions.Keys).ToArray();
	}

	/// <summary>Returns the transitions directly owned by <paramref name="behaviorName"/>.</summary>
	public string[] GetTransitionsFromBehavior(string behaviorName) =>
		GetTransitionNames(BehaviorTransitions, behaviorName);

	/// <summary>Returns whether <paramref name="behaviorName"/> exists in the behavior data.</summary>
	public bool HasBehavior(string behaviorName)
	{
		EnsureCollections();
		string key = BehaviorDataKeys.Normalize(behaviorName);
		return !string.IsNullOrEmpty(key) && BehaviorTransitions.ContainsKey(key);
	}

	/// <summary>Returns whether <paramref name="emitterName"/> exists in the behavior data.</summary>
	public bool HasEmitter(string emitterName)
	{
		EnsureCollections();
		string key = BehaviorDataKeys.Normalize(emitterName);
		return !string.IsNullOrEmpty(key) && EmitterTransitions.ContainsKey(key);
	}

	/// <summary>
	/// Returns the config Resource class name for <paramref name="behaviorName"/>,
	/// or empty when the behavior has no config capability.
	/// </summary>
	public string GetBehaviorConfigTypeName(string behaviorName) =>
		GetBehaviorCapabilityTypeName(BehaviorConfigTypes, behaviorName);

	/// <summary>
	/// Returns the board class name for <paramref name="behaviorName"/>,
	/// or empty when the behavior has no board capability.
	/// </summary>
	public string GetBehaviorBoardTypeName(string behaviorName) =>
		GetBehaviorCapabilityTypeName(BehaviorBoardTypes, behaviorName);

	internal static void WriteCapabilityTypeName(
		Godot.Collections.Dictionary<string, string> target,
		string behaviorName,
		Type? capabilityType)
	{
		if (capabilityType == null)
		{
			return;
		}

		target[behaviorName] = capabilityType.Name;
	}

	private string GetBehaviorCapabilityTypeName(
		Godot.Collections.Dictionary<string, string> typesByBehavior,
		string behaviorName)
	{
		EnsureCollections();
		string key = BehaviorDataKeys.Normalize(behaviorName);
		if (string.IsNullOrEmpty(key)
			|| !typesByBehavior.TryGetValue(key, out string? typeName)
			|| string.IsNullOrEmpty(typeName))
		{
			return string.Empty;
		}

		return typeName;
	}

	private static string[] GetTransitionNames(
		Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> transitionsByOwner,
		string ownerName)
	{
		string key = BehaviorDataKeys.Normalize(ownerName);
		if (string.IsNullOrEmpty(key)
			|| !transitionsByOwner.TryGetValue(key, out Godot.Collections.Array<string>? found)
			|| found == null)
		{
			return System.Array.Empty<string>();
		}

		return BehaviorDataKeys.FromStringArray(found);
	}

	private string[] GetKeysFromGroup(
		Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> groups,
		string groupName)
	{
		EnsureCollections();
		string key = BehaviorDataKeys.Normalize(groupName);
		if (string.IsNullOrEmpty(key)
			|| !groups.TryGetValue(key, out Godot.Collections.Array<string>? found)
			|| found == null)
		{
			return System.Array.Empty<string>();
		}

		return BehaviorDataKeys.FromStringArray(found);
	}

	private static bool TryFindOwnerInGroups(
		Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> groups,
		string normalizedOwnerKey,
		out string groupName)
	{
		foreach (KeyValuePair<string, Godot.Collections.Array<string>> entry in groups)
		{
			string[] keys = BehaviorDataKeys.FromStringArray(entry.Value);
			for (int i = 0; i < keys.Length; i++)
			{
				if (BehaviorDataKeys.KeysEqual(keys[i], normalizedOwnerKey))
				{
					groupName = entry.Key;
					return true;
				}
			}
		}

		groupName = string.Empty;
		return false;
	}
}

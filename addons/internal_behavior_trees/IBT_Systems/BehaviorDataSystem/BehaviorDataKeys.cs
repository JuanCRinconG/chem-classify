#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTSystem;

/// <summary>
/// String-key helpers for <see cref="IBTData"/> storage.
/// Data Path Maker edges convert with <see cref="ToStringNameArray"/> / <see cref="FromStringName"/>.
/// </summary>
public static class BehaviorDataKeys
{
	/// <summary>Group label for types declared in the global namespace.</summary>
	public const string GlobalGroupName = "Global";

	public static string Normalize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		return value.Trim();
	}

	public static string FromStringName(StringName value)
	{
		if (DataPathOperations.IsEmpty(value))
		{
			return string.Empty;
		}

		return Normalize(value.ToString());
	}

	public static bool IsEmpty(string? value) => string.IsNullOrEmpty(Normalize(value));

	public static bool KeysEqual(string? a, string? b) =>
		string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

	public static List<string> ListSortedKeys(IEnumerable<string> keys)
	{
		var list = new List<string>();
		foreach (string key in keys)
		{
			string normalized = Normalize(key);
			if (!string.IsNullOrEmpty(normalized))
			{
				list.Add(normalized);
			}
		}

		list.Sort(StringComparer.Ordinal);
		return list;
	}

	public static Godot.Collections.Array<StringName> ToStringNameArray(IEnumerable<string> keys)
	{
		var array = new Godot.Collections.Array<StringName>();
		foreach (string key in ListSortedKeys(keys))
		{
			array.Add(key);
		}

		return array;
	}

	/// <summary>Product group derived from a behavior type's C# namespace.</summary>
	public static string GetGroupName(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return string.IsNullOrEmpty(type.Namespace) ? GlobalGroupName : Normalize(type.Namespace);
	}

	/// <summary>
	/// Unique behavior-data key: <c>Namespace.TypeName</c>, or short name when global.
	/// </summary>
	public static string GetBehaviorKey(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		string shortName = Normalize(type.Name);
		if (string.IsNullOrEmpty(shortName))
		{
			return string.Empty;
		}

		return string.IsNullOrEmpty(type.Namespace)
			? shortName
			: $"{Normalize(type.Namespace)}.{shortName}";
	}

	/// <summary>Class name for UI display from a qualified behavior key.</summary>
	public static string GetBehaviorShortName(string behaviorKey)
	{
		string normalized = Normalize(behaviorKey);
		if (string.IsNullOrEmpty(normalized))
		{
			return string.Empty;
		}

		int separator = normalized.LastIndexOf('.');
		return separator < 0 ? normalized : normalized[(separator + 1)..];
	}

	public static Godot.Collections.Array<string> ToStringArray(IEnumerable<string> names)
	{
		var array = new Godot.Collections.Array<string>();
		foreach (string name in ListSortedKeys(names))
		{
			array.Add(name);
		}

		return array;
	}

	public static string[] FromStringArray(Godot.Collections.Array<string> array)
	{
		if (array == null || array.Count == 0)
		{
			return System.Array.Empty<string>();
		}

		var copy = new string[array.Count];
		for (int i = 0; i < array.Count; i++)
		{
			copy[i] = array[i] ?? string.Empty;
		}

		return copy;
	}
}

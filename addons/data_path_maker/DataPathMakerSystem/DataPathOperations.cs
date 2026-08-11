using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Shared <see cref="StringName"/> key and path helpers for Data Path Maker and consumers.
/// Path separator <c>/</c> is reserved in segments. Safe to extract for runtime shipping.
/// </summary>
public static class DataPathOperations
{	public static readonly StringName NoneTag = "None";

	public const char PathSeparator = '/';

	public static StringName Normalize(StringName value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return default;
		}

		var trimmed = value.ToString().Trim();
		return string.IsNullOrEmpty(trimmed) ? default : new StringName(trimmed);
	}

	public static bool IsEmpty(StringName value) => string.IsNullOrEmpty(value);

	public static bool KeysEqual(StringName a, StringName b)
	{
		var emptyA = IsEmpty(a);
		var emptyB = IsEmpty(b);
		if (emptyA && emptyB)
		{
			return true;
		}

		if (emptyA || emptyB)
		{
			return false;
		}

		return a.ToString() == b.ToString();
	}

	/// <summary>True when <paramref name="candidate"/> matches any key in <paramref name="keys"/> after <see cref="Normalize"/>.</summary>
	public static bool ContainsNormalized(IEnumerable<StringName> keys, StringName candidate)
	{
		if (keys == null || IsEmpty(candidate))
		{
			return false;
		}

		var normalized = Normalize(candidate);
		foreach (var key in keys)
		{
			if (Normalize(key) == normalized)
			{
				return true;
			}
		}

		return false;
	}

	public static StringName NormalizeOrNone(StringName value)
	{
		var normalized = Normalize(value);
		return normalized == NoneTag ? default : normalized;
	}

	public static void MergeStringNameKeys(IEnumerable<StringName> source, HashSet<StringName> destination)
	{
		if (source == null || destination == null)
		{
			return;
		}

		foreach (var key in source)
		{
			if (!IsEmpty(key))
			{
				destination.Add(key);
			}
		}
	}

	public static void SortStringNameList(List<StringName> keys)
	{
		if (keys == null)
		{
			return;
		}

		keys.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
	}

	public static Godot.Collections.Array<StringName> SortStringNameKeys(IEnumerable<StringName> keys)
	{
		var sorted = new List<StringName>(keys ?? Array.Empty<StringName>());
		sorted.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
		var result = new Godot.Collections.Array<StringName>();
		foreach (var key in sorted)
		{
			result.Add(key);
		}

		return result;
	}

	/// <summary>Splits a path on <see cref="PathSeparator"/>; empty / whitespace segments are dropped.</summary>
	public static List<StringName> SplitPath(StringName path)
	{
		var result = new List<StringName>();
		if (IsEmpty(path))
		{
			return result;
		}

		var parts = path.ToString().Split(PathSeparator, StringSplitOptions.None);
		for (var i = 0; i < parts.Length; i++)
		{
			var segment = Normalize(parts[i]);
			if (!IsEmpty(segment))
			{
				result.Add(segment);
			}
		}

		return result;
	}

	/// <summary>Joins normalized non-empty segments with <see cref="PathSeparator"/>.</summary>
	public static StringName JoinPath(IReadOnlyList<StringName> segments)
	{
		if (segments == null || segments.Count == 0)
		{
			return default;
		}

		var parts = new List<string>(segments.Count);
		for (var i = 0; i < segments.Count; i++)
		{
			var segment = Normalize(segments[i]);
			if (!IsEmpty(segment))
			{
				parts.Add(segment.ToString());
			}
		}

		return parts.Count == 0 ? default : new StringName(string.Join(PathSeparator, parts));
	}

	public static StringName GetPathSegment(StringName path, int index)
	{
		if (index < 0)
		{
			return default;
		}

		var segments = SplitPath(path);
		return index < segments.Count ? segments[index] : default;
	}

	public static int PathSegmentCount(StringName path) => SplitPath(path).Count;
}
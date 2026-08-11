using System.Collections.Generic;
using Godot;

/// <summary>
/// Descend walk over path segments: consume with <see cref="TryNext"/>,
/// list from the current host when <see cref="Done"/>.
/// Reference type so <see cref="DataPickerSite.TryInvoke"/> can detect leftover segments
/// after the query returns. Reads only — results stay on <see cref="DataQueryResult"/>.
/// </summary>
public sealed class PathWalk
{
	private static readonly List<StringName> EmptyPath = new();

	private readonly List<StringName> _path;
	private int _index;

	public PathWalk(List<StringName> pathSoFar)
	{
		_path = pathSoFar ?? EmptyPath;
		_index = 0;
	}

	/// <summary>True when every segment has been consumed.</summary>
	public bool Done => _index >= _path.Count;

	/// <summary>
	/// Requires the next normalized non-empty segment and advances.
	/// On failure, <paramref name="failure"/> is a ready <see cref="DataQueryResult.Fail"/>.
	/// </summary>
	public bool TryNext(
		out StringName segment,
		out DataQueryResult failure,
		string missingMessage = null)
	{
		segment = default;
		failure = default;

		if (_index >= _path.Count)
		{
			failure = DataQueryResult.Fail(missingMessage ?? "Path segment is missing.");
			return false;
		}

		segment = DataPathOperations.Normalize(_path[_index]);
		if (DataPathOperations.IsEmpty(segment))
		{
			failure = DataQueryResult.Fail(missingMessage ?? "Path segment is empty.");
			return false;
		}

		_index++;
		return true;
	}
}

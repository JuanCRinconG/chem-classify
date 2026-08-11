using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Result of applying a Data Path Maker query against the session Resource.
/// </summary>
public readonly struct DataQueryResult
{
	private const string EmptyKeysFailureReason =
		"Query returned zero keys after normalization; empty success is not allowed.";

	/// <summary>Default when a list step finds no keys on an otherwise valid host.</summary>
	public const string DefaultEmptyCatalogueFailure = "Correct resource, empty data.";

	/// <summary>Default when the path still has segments after the query finishes its walk.</summary>
	public const string DefaultPathTooLongFailure = "Path too long for this query.";

	/// <summary>Keys offered for the next path segment (empty keys should be stripped by the query).</summary>
	public Godot.Collections.Array<StringName> NextKeys { get; init; }

	/// <summary>
	/// When true, choosing one of <see cref="NextKeys"/> completes the editable path and commits.
	/// When false, the pick is appended and the query is applied again with the longer path.
	/// </summary>
	public bool IsTerminalStep { get; init; }

	/// <summary>Non-empty when the query cannot run (wrong root, incomplete path, etc.).</summary>
	public string FailureReason { get; init; }

	public bool Ok => string.IsNullOrEmpty(FailureReason);

	public static DataQueryResult Fail(string reason) =>
		new()
		{
			NextKeys = new Godot.Collections.Array<StringName>(),
			IsTerminalStep = false,
			FailureReason = reason ?? "Query failed.",
		};

	public static DataQueryResult Keys(
		Godot.Collections.Array<StringName> nextKeys,
		bool isTerminalStep) =>
		new()
		{
			NextKeys = nextKeys ?? new Godot.Collections.Array<StringName>(),
			IsTerminalStep = isTerminalStep,
			FailureReason = null,
		};

	/// <summary>
	/// Returns <see cref="Keys"/> when <paramref name="keys"/> has at least one entry;
	/// otherwise <see cref="Fail"/> with <paramref name="emptyFailure"/>.
	/// </summary>
	public static DataQueryResult KeysOrFail(
		Godot.Collections.Array<StringName> keys,
		bool isTerminalStep,
		string emptyFailure) =>
		keys == null || keys.Count == 0
			? Fail(string.IsNullOrEmpty(emptyFailure) ? DefaultEmptyCatalogueFailure : emptyFailure)
			: Keys(keys, isTerminalStep);

	/// <summary>Non-terminal list step; empty keys → <see cref="DefaultEmptyCatalogueFailure"/> unless overridden.</summary>
	public static DataQueryResult NonTerminal(
		Godot.Collections.Array<StringName> keys,
		string emptyFailure = null) =>
		KeysOrFail(keys, isTerminalStep: false, emptyFailure);

	/// <summary>Terminal list step; empty keys → <see cref="DefaultEmptyCatalogueFailure"/> unless overridden.</summary>
	public static DataQueryResult Terminal(
		Godot.Collections.Array<StringName> keys,
		string emptyFailure = null) =>
		KeysOrFail(keys, isTerminalStep: true, emptyFailure);

	/// <summary>
	/// Requires a resolved host object. Null → <see cref="Fail"/> with <paramref name="unknownFailure"/>.
	/// </summary>
	public static bool TryHost<T>(T host, string unknownFailure, out DataQueryResult failure)
		where T : class
	{
		if (host != null)
		{
			failure = default;
			return true;
		}

		failure = Fail(unknownFailure ?? "Unknown path host.");
		return false;
	}

	/// <summary>Strips empty keys; empty success becomes <see cref="Fail"/>.</summary>
	public DataQueryResult Normalized()
	{
		if (!Ok)
		{
			return this;
		}

		var raw = NextKeys;
		var cleaned = new Godot.Collections.Array<StringName>();
		if (raw != null)
		{
			for (var i = 0; i < raw.Count; i++)
			{
				var key = DataPathOperations.Normalize(raw[i]);
				if (!DataPathOperations.IsEmpty(key))
				{
					cleaned.Add(key);
				}
			}
		}

		if (cleaned.Count == 0)
		{
			return Fail(EmptyKeysFailureReason);
		}

		return new DataQueryResult
		{
			NextKeys = cleaned,
			IsTerminalStep = IsTerminalStep,
			FailureReason = null,
		};
	}
}

/// <summary>
/// Picker site bound from an Export hint string (<c>DataPicker/MethodName</c>).
/// Owns lazy invoke-cache keyed by session root type + <see cref="QueryMethod"/>.
/// </summary>
public readonly struct DataPickerSite
{
	private static readonly ConcurrentDictionary<(Type RootType, string Method), MethodInfo> InvokeCache = new();

	public DataPickerSite(string queryMethod)
	{
		QueryMethod = queryMethod ?? string.Empty;
	}

	/// <summary>Instance query method name on the session root type.</summary>
	public string QueryMethod { get; }

	public bool IsValid => !string.IsNullOrEmpty(QueryMethod);

	/// <summary>
	/// Builds a site from Godot Export hint metadata.
	/// Expects <paramref name="hintType"/> <see cref="PropertyHint.None"/> and a hint string
	/// starting with <see cref="DataPathMakerVariables.DataPicker"/>.
	/// </summary>
	public static bool TryFromHintString(PropertyHint hintType, string hintString, out DataPickerSite site)
	{
		site = default;
		if (hintType != PropertyHint.None || string.IsNullOrEmpty(hintString))
		{
			return false;
		}

		var prefix = DataPathMakerVariables.DataPicker;
		if (!hintString.StartsWith(prefix, StringComparison.Ordinal))
		{
			return false;
		}

		var methodName = hintString.Substring(prefix.Length).Trim();
		if (string.IsNullOrEmpty(methodName))
		{
			return false;
		}

		site = new DataPickerSite(methodName);
		return true;
	}

	/// <summary>
	/// Soft gate: session present and query method resolves on the root's type. Does not run the query.
	/// </summary>
	public bool CanServe(Resource root, out string failureReason)
	{
		failureReason = null;
		if (root == null)
		{
			failureReason = "No session resource. Use Change Data to load a Resource root.";
			return false;
		}

		return TryResolve(root.GetType(), out failureReason);
	}

	/// <summary>Resolves and caches the instance query <see cref="MethodInfo"/> for this site (lazy).</summary>
	public bool TryResolve(Type rootType, out string failureReason)
	{
		failureReason = null;

		if (!IsValid)
		{
			failureReason = "Data Path Maker: QueryMethod is empty.";
			return false;
		}

		if (rootType == null)
		{
			failureReason = "Data Path Maker: Session root type is unbound.";
			return false;
		}

		var cacheKey = (rootType, QueryMethod);
		if (InvokeCache.ContainsKey(cacheKey))
		{
			return true;
		}

		if (!TryCreateInvoke(rootType, QueryMethod, out var method, out failureReason))
		{
			return false;
		}

		InvokeCache[cacheKey] = method;
		return true;
	}

	/// <summary>Resolve (if needed) and run the query against <paramref name="root"/>.</summary>
	public bool TryInvoke(
		Resource root,
		List<StringName> pathSoFar,
		out DataQueryResult result,
		out string failureReason)
	{
		result = default;

		if (root == null)
		{
			result = DataQueryResult.Fail(
				"No session resource. Use Change Data to load a Resource root.");
			failureReason = result.FailureReason;
			return false;
		}

		var rootType = root.GetType();
		if (!TryResolve(rootType, out failureReason))
		{
			return false;
		}

		var walk = new PathWalk(pathSoFar ?? new List<StringName>());
		var method = InvokeCache[(rootType, QueryMethod)];
		try
		{
			result = (DataQueryResult)method.Invoke(root, new object[] { walk });
		}
		catch (TargetInvocationException ex)
		{
			var message = ex.InnerException?.Message ?? ex.Message;
			result = DataQueryResult.Fail($"Query '{QueryMethod}' threw: {message}");
			failureReason = result.FailureReason;
			return false;
		}
		catch (Exception ex)
		{
			result = DataQueryResult.Fail($"Query '{QueryMethod}' threw: {ex.Message}");
			failureReason = result.FailureReason;
			return false;
		}

		result = result.Normalized();
		if (result.Ok && !walk.Done)
		{
			result = DataQueryResult.Fail(DataQueryResult.DefaultPathTooLongFailure);
		}

		if (!result.Ok)
		{
			failureReason = result.FailureReason ?? "Picker query failed against the current session Resource.";
			return false;
		}

		failureReason = null;
		return true;
	}

	/// <summary>Clears cached query methods (tests / hot reload).</summary>
	public static void ResetCache() => InvokeCache.Clear();

	private static bool TryCreateInvoke(
		Type rootType,
		string queryMethod,
		out MethodInfo method,
		out string failureReason)
	{
		method = null;
		failureReason = null;

		const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
		foreach (var candidate in rootType.GetMethods(flags))
		{
			if (!string.Equals(candidate.Name, queryMethod, StringComparison.Ordinal))
			{
				continue;
			}

			if (!IsQuerySignature(candidate))
			{
				continue;
			}

			method = candidate;
			break;
		}

		if (method == null)
		{
			failureReason =
				$"Data Path Maker: instance query '{rootType.Name}.{queryMethod}' not found on session root. "
				+ "Expected: DataQueryResult Method(PathWalk).";
			return false;
		}

		return true;
	}

	private static bool IsQuerySignature(MethodInfo method)
	{
		if (method == null || method.ReturnType != typeof(DataQueryResult))
		{
			return false;
		}

		var parameters = method.GetParameters();
		if (parameters.Length != 1)
		{
			return false;
		}

		return parameters[0].ParameterType == typeof(PathWalk);
	}
}

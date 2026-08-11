#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ISSystem;

/// <summary>
/// Attribute-driven enum-to-instance factory. Resolves tagged state classes; misconfiguration fails at runtime.
/// </summary>
public static class InternalStateManager
{
	private static readonly Lazy<ILookup<Type, (Type ImplType, object KeyValue)>> FoundStateAttributes = new(ScanForStateAttributes);

	private static readonly Lazy<IReadOnlyDictionary<(Type KeyType, object KeyValue), Func<object>>> StateFactories = new(BuildStateFactories);

	private static readonly ConditionalWeakTable<object, Dictionary<(Type, object), object>> OwnerStateCaches = new();

	/// <summary>
	/// Returns the state registered for <paramref name="selectedKey"/>, cached per <paramref name="owner"/>.
	/// The enum type is taken from <paramref name="selectedKey"/> (same as <see cref="StateAttribute"/> key typing).
	/// </summary>
	public static T GetState<T>(object owner, Enum selectedKey) where T : class
	{
		Dictionary<(Type, object), object> ownerCache =
			OwnerStateCaches.GetValue(owner, _ => new());

		var cacheKey = (selectedKey.GetType(), (object)selectedKey);

		if (ownerCache.TryGetValue(cacheKey, out object? existing))
		{
			return (T)existing;
		}

		object created = StateFactories.Value[cacheKey]();
		ownerCache[cacheKey] = created;
		return (T)created;
	}

	public static T CreateState<T>(Enum selectedKey) where T : class
	{
		var cacheKey = (selectedKey.GetType(), (object)selectedKey);
		return (T)StateFactories.Value[cacheKey]();
	}

	private static ILookup<Type, (Type ImplType, object KeyValue)> ScanForStateAttributes()
	{
		Assembly assembly = typeof(StateAttribute).Assembly;
		var entries = new List<(Type KeyType, Type ImplType, object KeyValue)>();

		foreach (Type type in assembly.GetTypes())
		{
			if (type.IsAbstract || type.IsInterface)
			{
				continue;
			}

			StateAttribute? attr = type.GetCustomAttribute<StateAttribute>(inherit: false);
			if (attr == null)
			{
				continue;
			}

			entries.Add((attr.KeyType, type, attr.KeyValue));
		}

		return entries.ToLookup(e => e.KeyType, e => (e.ImplType, e.KeyValue));
	}

	private static IReadOnlyDictionary<(Type KeyType, object KeyValue), Func<object>> BuildStateFactories()
	{
		var factories = new Dictionary<(Type KeyType, object KeyValue), Func<object>>();

		foreach (IGrouping<Type, (Type ImplType, object KeyValue)> keyGroup in FoundStateAttributes.Value)
		{
			Type keyType = keyGroup.Key;
			foreach ((Type implType, object keyValueObj) in keyGroup)
			{
				var lookupKey = (keyType, keyValueObj);
				if (factories.ContainsKey(lookupKey))
				{
					throw new InvalidOperationException(
						$"InternalStateManager: duplicate key '{keyValueObj}' on {implType.Name}.");
				}

				Type implementationType = implType;
				factories[lookupKey] = () => Activator.CreateInstance(implementationType)!;
			}
		}

		return factories;
	}
}

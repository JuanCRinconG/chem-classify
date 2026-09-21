using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// Marks an exported resource reference that should always have an in-memory fallback.
/// Nested marked resource properties are created too, so one behavior call can prepare a
/// whole preset graph.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AutoCreateResourceAttribute : Attribute
{
	public AutoCreateResourceAttribute(bool includeNestedResources = true)
	{
		IncludeNestedResources = includeNestedResources;
	}

	public bool IncludeNestedResources { get; }
}

/// <summary>
/// Shared resource guard for behavior presets. Add <see cref="AutoCreateResourceAttribute"/>
/// to resource properties that must never be null, then call <see cref="EnsurePresetResources"/>
/// once from the behavior's setup path.
/// </summary>
public static class ResourceApplier
{
	private const BindingFlags PublicInstanceProperties = BindingFlags.Public | BindingFlags.Instance;

	public static void EnsurePresetResources(this object target)
	{
		if (target == null)
		{
			return;
		}

		EnsurePresetResources(target, new HashSet<object>(ReferenceComparer.Instance), target.GetType().Name);
	}

	private static void EnsurePresetResources(object target, HashSet<object> visited, string ownerPath)
	{
		if (!visited.Add(target))
		{
			return;
		}

		foreach (PropertyInfo property in target.GetType().GetProperties(PublicInstanceProperties))
		{
			AutoCreateResourceAttribute attribute = property.GetCustomAttribute<AutoCreateResourceAttribute>();
			if (attribute == null)
			{
				continue;
			}

			if (property.GetIndexParameters().Length > 0)
			{
				continue;
			}

			if (!typeof(Resource).IsAssignableFrom(property.PropertyType))
			{
				GD.PushWarning(
					$"{nameof(AutoCreateResourceAttribute)} can only be used on Resource properties. " +
					$"Ignoring {ownerPath}.{property.Name}.");
				continue;
			}

			if (!property.CanRead || !property.CanWrite)
			{
				GD.PushWarning(
					$"{ownerPath}.{property.Name} must have both a getter and setter to be auto-created.");
				continue;
			}

			Resource resource = property.GetValue(target) as Resource;
			if (resource == null)
			{
				resource = CreateResource(property.PropertyType, $"{ownerPath}.{property.Name}");
				if (resource == null)
				{
					continue;
				}

				property.SetValue(target, resource);
			}

			if (attribute.IncludeNestedResources)
			{
				EnsurePresetResources(resource, visited, $"{ownerPath}.{property.Name}");
			}
		}
	}

	private static Resource CreateResource(Type resourceType, string propertyPath)
	{
		try
		{
			return Activator.CreateInstance(resourceType) as Resource;
		}
		catch (Exception exception)
		{
			GD.PushError(
				$"Could not create fallback resource for {propertyPath} ({resourceType.Name}): " +
				exception.Message);
			return null;
		}
	}

	private sealed class ReferenceComparer : IEqualityComparer<object>
	{
		public static readonly ReferenceComparer Instance = new ReferenceComparer();

		bool IEqualityComparer<object>.Equals(object x, object y)
		{
			return ReferenceEquals(x, y);
		}

		int IEqualityComparer<object>.GetHashCode(object obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}
}

#nullable enable
using System;

namespace ISSystem;

/// <summary>
/// Maps a state implementation class to its enum registry key for <see cref="InternalStateManager"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StateAttribute : Attribute
{
	public Type KeyType { get; }

	public object KeyValue { get; }

	public StateAttribute(object keyValue)
	{
		ArgumentNullException.ThrowIfNull(keyValue);

		KeyValue = keyValue;
		KeyType = keyValue.GetType();
	}
}

#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTSystem;

/// <summary>
/// One behavior instance on an <see cref="IBTChart"/>. Instance identity is the chart
/// dictionary key. Extends <see cref="TransitionNode"/> with hierarchy, process order,
/// configuration, and board binding.
/// </summary>
[Tool]
[GlobalClass]
public partial class BehaviorNode : TransitionNode
{
	/// <summary>
	/// Direct child instance IDs in chart order. A node with no parent is a chart root.
	/// </summary>
	[Export]
	public string[] ChildBehaviorInstanceIds { get; set; } = Array.Empty<string>();

	[Export]
	public BehaviorProcessOrder ProcessOrder { get; set; } = BehaviorProcessOrder.NoChildProcess;

	/// <summary>
	/// Optional untyped designer payload injected into a compatible
	/// <see cref="BehaviorConfig{TConfig}"/> during engine build.
	/// </summary>
	[Export]
	public Resource? Config { get; set; }

	/// <summary>
	/// Host board binding group matched during <see cref="BehaviorEngine"/> board registration.
	/// </summary>
	[Export]
	public StringName BoardGroup { get; set; } = BehaviorEngine.DefaultGroupName;

	/// <summary>
	/// Applies a data-validated behavior replacement without changing this
	/// instance's identity, hierarchy, process order, position, or incoming wires.
	/// </summary>
	public void ApplyBehaviorTypeChange(
		string typeName,
		string configTypeName,
		IEnumerable<string> availableTransitionNames)
	{
		if (Config != null && !string.Equals(Config.GetClass(), configTypeName, StringComparison.Ordinal))
		{
			Config = null;
		}

		ApplyFullTypeChange(typeName, availableTransitionNames);
	}
}

#nullable enable
using Godot;

namespace IBTSystem;

/// <summary>
/// Emitter transition hub on an <see cref="IBTChart"/>, keyed by instance ID in
/// <see cref="IBTChart.TransitionEngineNodes"/>. Multiple engines may share an emitter
/// type when their <see cref="BindingGroup"/> values differ.
/// </summary>
[Tool]
[GlobalClass]
public partial class TransitionEngineNode : TransitionNode
{
	/// <summary>Runtime binding group matched by the host when wiring external transitions.</summary>
	[Export]
	public string? BindingGroup { get; set; }
}

#nullable enable
using System.Collections.Generic;
using Godot;

namespace ISSystem;

/// <summary>
/// Optional enter/exit lifecycle. Default methods are no-ops; override in states that need setup.
/// </summary>
public interface CycleState<TContext>
{
	void OnEnter(TContext context) { }

	void OnExit(TContext context) { }
}

/// <summary>
/// Physics-step processing with <c>double</c> delta time.
/// </summary>
public interface DeltaProcessState<TContext>
{
	void Process(TContext context, double delta);
}

/// <summary>
/// Maps raw movement input (XZ axes) into world-space movement for the context owner.
/// </summary>
public interface MovementInputTransformState<TContext>
{
	Vector2 TransformMovementInput(TContext context, Vector2 rawInput);
}

/// <summary>
/// Heterogeneous per-tick/event processing via <see cref="Variant"/> dependency.
/// </summary>
public interface VariantProcessState<TContext>
{
	Variant Process(TContext context, Variant dependency);
}

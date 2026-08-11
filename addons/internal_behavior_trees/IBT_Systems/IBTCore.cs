using Godot;
using System;
using System.Collections.Generic;

namespace IBTSystem;

/// <summary>
/// Marks a behavior class for <see cref="IBTManager"/>.
/// Product group is inferred from the declaring type's C# namespace at scan time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BehaviorAttribute : Attribute
{
}

/// <summary>
/// Marks a transition event for <see cref="IBTManager"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Event, AllowMultiple = false, Inherited = false)]
public sealed class BehaviorTransitionAttribute : Attribute
{
}

public abstract class BehaviorCore
{
	/// <summary>Chart behavior instance ID this core was built from.</summary>
	public string ChartInstanceId = string.Empty;
	public BehaviorProcessOrder CurrentProcessOrder { get; private set; }
	private BehaviorProcessOrderMode _processOrderMode = null!;
	public List<BehaviorCore> Children { get; } = new();

	public virtual void OnEnter() { }

	public virtual void OnExit() { }

	public virtual void Process(double delta) { }

	internal void SetProcessOrder(BehaviorProcessOrder processOrder)
	{
		_processOrderMode = InternalStateManager.GetState<BehaviorProcessOrderMode>(this, processOrder);
		CurrentProcessOrder = processOrder;
	}

	internal void EngineEnter()
	{
		OnEnter();
		for (int i = 0; i < Children.Count; i++)
		{
			Children[i].EngineEnter();
		}
	}

	internal void EngineExit()
	{
		for (int i = 0; i < Children.Count; i++)
		{
			Children[i].EngineExit();
		}
		OnExit();
	}

	internal void EngineProcess(double delta)
	{
		_processOrderMode.Process(this, delta);
	}

	internal void ProcessChildren(double delta)
	{
		for (int i = 0; i < Children.Count; i++)
		{
			Children[i].EngineProcess(delta);
		}
	}
}

#region Behavior Process Order

/// <summary>
/// Designer-selected order for processing a behavior and all of its direct child subtrees.
/// </summary>
public enum BehaviorProcessOrder
{
	NoChildProcess,
	ParentBeforeChild,
	ParentAfterChild
}

public abstract class BehaviorProcessOrderMode
{
	public abstract void Process(BehaviorCore behavior, double delta);
}

[State(BehaviorProcessOrder.NoChildProcess)]
public sealed class NoChildProcessMode : BehaviorProcessOrderMode
{
	public override void Process(BehaviorCore behavior, double delta)
	{
		behavior.Process(delta);
	}
}

[State(BehaviorProcessOrder.ParentBeforeChild)]
public sealed class ParentBeforeChildMode : BehaviorProcessOrderMode
{
	public override void Process(BehaviorCore behavior, double delta)
	{
		behavior.Process(delta);
		behavior.ProcessChildren(delta);
	}
}

[State(BehaviorProcessOrder.ParentAfterChild)]
public sealed class ParentAfterChildMode : BehaviorProcessOrderMode
{
	public override void Process(BehaviorCore behavior, double delta)
	{
		behavior.ProcessChildren(delta);
		behavior.Process(delta);
	}
}

#endregion

/// <summary>
/// Optional designer config injection. Soft scan / chart <c>Config</c> resolve
/// <typeparamref name="TConfig"/> from this interface — not from <see cref="BehaviorAttribute"/>.
/// Omit entirely when the type has no designer payload.
/// </summary>
public interface BehaviorConfig<TConfig> where TConfig : Resource
{
	TConfig Config { get; set; }
}

/// <summary>
/// Optional typed board reference. The host supplies instances through
/// <see cref="BehaviorEngine.SetBoards"/>, matched by chart <see cref="BehaviorNode.BoardGroup"/>.
/// </summary>
public interface BehaviorBoard<TBoard> where TBoard : class
{
	TBoard Board { get; set; }
}

/// <summary>
/// Convenience behavior base with typed board and config injection.
/// </summary>
public abstract partial class IBTBehavior<TBoard, TConfig> : BehaviorCore,
	BehaviorBoard<TBoard>,
	BehaviorConfig<TConfig>
	where TBoard : class
	where TConfig : Resource
{
	public TBoard Board { get; set; } = null!;
	public TConfig Config { get; set; } = null!;
}

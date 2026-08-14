#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTSystem;

/// <summary>Runtime notification payload when the active behavior root changes via a transition wire.</summary>
public readonly struct BehaviorTransitionEvent
{
	public string OwnerName { get; init; }
	public string TransitionName { get; init; }
	public string FromInstanceId { get; init; }
	public string ToInstanceId { get; init; }
	public string Message { get; init; }

	public string MessageRedux { get; init; }
}

/// <summary>
/// Live FSM: explicit <see cref="Start"/> / <see cref="Reset"/>, subtree Enter/Exit on the
/// active <see cref="BehaviorCore"/>, and one-line <see cref="Process"/> dispatch.
/// </summary>
public partial class BehaviorEngine
{
	private BehaviorCore? _initialBehavior;
	private BehaviorCore? _activeBehavior;
	private readonly Queue<(string OwnerName, string TransitionName, BehaviorCore Destination)>
		_pendingTransitions = new(capacity: 4);
	private bool _cascadeBusy;

	/// <summary>
	/// True after a successful <see cref="Start"/> until <see cref="Reset"/>.
	/// </summary>
	public bool IsStarted { get; private set; }

	/// <summary>Active behavior data type name, or empty when not started / after reset.</summary>
	public string ActiveTypeName => _activeBehavior?.GetType().Name ?? string.Empty;

	/// <summary>
	/// Active behavior chart instance ID, or empty when not started / after reset.
	/// Cross-check against <see cref="IBTChart.BehaviorNodes"/> keys at runtime.
	/// </summary>
	public string ActiveBehaviorInstanceId => _activeBehavior?.ChartInstanceId ?? string.Empty;

	/// <summary>Active behavior core, or null when not started / after reset.</summary>
	public BehaviorCore? ActiveBehavior => _activeBehavior;

	/// <summary>
	/// Fired when the active chart behavior instance changes.
	/// Argument is <see cref="ActiveBehaviorInstanceId"/> (empty when not started / after reset).
	/// </summary>
	public event Action<string>? ActiveBehaviorInstanceChanged;

	/// <summary>
	/// Fired after each applied root transition (including cascaded same-frame transitions).
	/// Independent of <see cref="Debug"/>; use for live graph tooling.
	/// </summary>
	public event Action<BehaviorTransitionEvent>? TransitionOccurred;

	private void NotifyActiveBehaviorInstanceChanged() =>
		ActiveBehaviorInstanceChanged?.Invoke(ActiveBehaviorInstanceId);

	private void NotifyTransitionOccurred(in BehaviorTransitionEvent transitionEvent) =>
		TransitionOccurred?.Invoke(transitionEvent);

	/// <summary>
	/// Enters the chart initial behavior subtree.
	/// Call after <see cref="Build"/>, <see cref="SetBoards"/>, and <see cref="SetExternalTransitions"/>.
	/// Starts at most once until <see cref="Reset"/>.
	/// </summary>
	public void Start()
	{
		if (!IsBuilt)
		{
			GD.PushWarning("BehaviorEngine.Start: engine is not built; start skipped.");
			return;
		}

		if (IsStarted)
		{
			GD.PushWarning("BehaviorEngine.Start: already started; call Reset before Start again.");
			return;
		}

		if (_initialBehavior == null)
		{
			GD.PushWarning(
				"BehaviorEngine.Start: InitialBehaviorInstanceId is empty or not included on this chart; start skipped.");
			return;
		}

		_activeBehavior = _initialBehavior;
		_activeBehavior.EngineEnter();
		IsStarted = true;
		NotifyActiveBehaviorInstanceChanged();
	}

	/// <summary>
	/// Exits the active behavior subtree and clears started state.
	/// Does not unbind external transitions or destroy instances — call <see cref="Dispose"/> separately if needed.
	/// </summary>
	public void Reset()
	{
		if (IsStarted && _activeBehavior != null)
		{
			_activeBehavior.EngineExit();
		}

		_activeBehavior = null;
		_pendingTransitions.Clear();
		_cascadeBusy = false;
		IsStarted = false;
		NotifyActiveBehaviorInstanceChanged();
	}

	/// <summary>
	/// Stops the engine, unbinds abstract contexts, and clears build state so this instance
	/// can be rebuilt without allocating a new <see cref="BehaviorEngine"/>.
	/// Typical chart swap:
	/// <c>Dispose(); Build(chart); SetBoards((host, null)); SetExternalTransitions((host, null)); Start();</c>
	/// </summary>
	public void Dispose()
	{
		Reset();
		UnbindAllExternalTransitions();
		WipeBuildState();
	}

	/// <summary>
	/// Host one-liner for <c>_Process</c> / <c>_PhysicsProcess</c>.
	/// Dispatches only the active behavior. Child traversal belongs to <see cref="BehaviorCore"/>.
	/// </summary>
	public void Process(double delta)
	{
		if (!IsStarted || _activeBehavior == null)
		{
			return;
		}

		_activeBehavior.EngineProcess(delta);
	}

	/// <summary>Behavior-based transition used by wired signal bridges after Capture.</summary>
	internal void TransitionTo(string ownerName, string transitionName, BehaviorCore destination)
	{
		LogSignalReceived(ownerName, transitionName);

		if (!IsStarted)
		{
			GD.PushWarning("BehaviorEngine.TransitionTo: not started; transition ignored.");
			return;
		}

		if (destination == null)
		{
			GD.PushWarning("BehaviorEngine.TransitionTo: destination is null; transition ignored.");
			return;
		}

		if (_cascadeBusy)
		{
			_pendingTransitions.Enqueue((ownerName, transitionName, destination));
			return;
		}

		ApplyTransition(ownerName, transitionName, destination);

		while (_pendingTransitions.Count > 0)
		{
			(string pendingOwner, string pendingTransition, BehaviorCore pendingDestination) =
				_pendingTransitions.Dequeue();
			ApplyTransition(pendingOwner, pendingTransition, pendingDestination);
		}
	}

	private void ApplyTransition(string ownerName, string transitionName, BehaviorCore destination)
	{
		if (ReferenceEquals(_activeBehavior, destination))
		{
			return;
		}

		_cascadeBusy = true;
		try
		{
			string fromInstanceId = _activeBehavior?.ChartInstanceId ?? string.Empty;
			string message = $"{ownerName}.{transitionName} → {destination.GetType().Name}";
			LogTransitionApplied(message);

			_activeBehavior?.EngineExit();
			destination.EngineEnter();
			_activeBehavior = destination;
			NotifyTransitionOccurred(new BehaviorTransitionEvent
			{
				OwnerName = ownerName,	
				TransitionName = transitionName,
				FromInstanceId = fromInstanceId,
				ToInstanceId = destination.ChartInstanceId,
				Message = message,
				MessageRedux = $"{transitionName} → {destination.GetType().Name}",
			});
			NotifyActiveBehaviorInstanceChanged();
		}
		finally
		{
			_cascadeBusy = false;
		}
	}
}

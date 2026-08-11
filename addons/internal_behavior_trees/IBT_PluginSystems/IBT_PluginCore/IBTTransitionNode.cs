#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace IBTPlugin;

/// <summary>Shared transition-output wiring for chart graph nodes.</summary>
[Tool]
public partial class IBTTransitionNode : GraphNode
{
	/// <summary>Scene contract for transition-engine nodes (must match InitialOutputSlot in the .tscn).</summary>
	public const int DefaultInitialOutputSlot = 1;

	[Export]
	public int InitialOutputSlot { get; set; } = DefaultInitialOutputSlot;

	[Export]
	public Color OutputColor { get; set; } = new(1f, 0.76f, 0.32f);

	[Export]
	public Theme TransitionLabelTheme { get; set; } = null!;

	private readonly Dictionary<int, string> _transitionNameByPort = new();
	private readonly List<Control> _transitionOutputRows = new();

	public IBTSystem.TransitionNode? CurrentNode { get; set; }
	public string InstanceId { get; protected set; } = string.Empty;

	public Action<IBTData?>? AfterRefreshFromChart { get; set; }

	public virtual IBTSystem.TransitionNode? TransitionNode => CurrentNode;

	/// <summary>First GraphEdit output port index used by transition outputs on this node.</summary>
	protected virtual int FirstTransitionOutputPort => 0;

	protected virtual string TransitionTooltipPrefix => "Emitter transition";

	/// <summary>Looks up a transition by its GraphEdit output port index.</summary>
	public bool TryGetTransitionName(long outputPort, out string transitionName) =>
		_transitionNameByPort.TryGetValue((int)outputPort, out transitionName!);

	public bool TryFindTransitionOutputPort(string transitionName, out int outputPort)
	{
		for (int port = FirstTransitionOutputPort; ; port++)
		{
			if (TryGetTransitionName(port, out string found)
				&& string.Equals(found, transitionName, StringComparison.Ordinal))
			{
				outputPort = port;
				return true;
			}

			if (!TryGetTransitionName(port, out _))
			{
				break;
			}
		}

		outputPort = default;
		return false;
	}

	public void BindFromChart(string instanceId, TransitionEngineNode chartNode)
	{
		InstanceId = instanceId;
		CurrentNode = chartNode;
		Name = BehaviorChartGraphNaming.TransitionEngineNode(instanceId);
	}

	public virtual void RefreshFromChart(string instanceId, IBTData? data)
	{
		if (CurrentNode is not TransitionEngineNode engineNode)
		{
			return;
		}

		InstanceId = instanceId;
		Name = BehaviorChartGraphNaming.TransitionEngineNode(instanceId);
		Title = engineNode.FullTypeName;
		ApplyTransitionOutputPorts(
			BehaviorChartTransitionNames.ForEmitter(data, engineNode));
		AfterRefreshFromChart?.Invoke(data);
	}

	protected void ApplyTransitionOutputPorts(string[] transitionNames)
	{
		int previousRowCount = _transitionOutputRows.Count;
		for (int i = 0; i < previousRowCount; i++)
		{
			ClearSlot(InitialOutputSlot + i);
			RemoveChild(_transitionOutputRows[i]);
			_transitionOutputRows[i].QueueFree();
		}

		_transitionOutputRows.Clear();
		_transitionNameByPort.Clear();
		for (int i = 0; i < transitionNames.Length; i++)
		{
			int slot = InitialOutputSlot + i;
			int outputPort = FirstTransitionOutputPort + i;
			var row = new Label
			{
				Text = transitionNames[i],
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				TooltipText = $"{TransitionTooltipPrefix} '{transitionNames[i]}' destination",
				Theme = TransitionLabelTheme,
			};
			AddChild(row);
			SetSlot(
				slot,
				false,
				0,
				Colors.Transparent,
				true,
				(int)IBTGraphConnectionType.TransitionOutput,
				OutputColor,
				null,
				null);
			_transitionOutputRows.Add(row);
			_transitionNameByPort.Add(outputPort, transitionNames[i]);
		}

		ResetSize();
	}
}

#nullable enable
using System;
using Godot;
using IBTSystem;

namespace IBTPlugin;

/// <summary>Behavior chart node with hierarchy ports and runtime/editor preset controls.</summary>
[Tool]
public partial class IBTBehaviorNode : IBTTransitionNode
{
	/// <summary>GraphEdit port index for the 1:N parent/child hierarchy connection.</summary>
	public const int ParentChildPort = 0;

	/// <summary>GraphEdit input port index for transition destinations.</summary>
	public const int TransitionInputPort = 1;

	protected override int FirstTransitionOutputPort => 1;

	/// <summary>Scene contract: transition output rows begin here (must match InitialOutputSlot in the .tscn).</summary>
	public new const int DefaultInitialOutputSlot = 2;

	[Export]
	public OptionButton ProcessOrderPicker { get; set; } = null!;

	[Export]
	public CheckBox SetAsInitialButton { get; set; } = null!;

	public Action? RequestChartMutation { get; set; }

	protected override string TransitionTooltipPrefix => "Transition";

	[Export]
	public BehaviorProcessOrder ProcessOrderWhenChildren { get; set; } = BehaviorProcessOrder.ParentBeforeChild;

	private readonly IBTBindings _uiBindings = new();
	private bool _suppressProcessOrderChanged;

	internal BehaviorNode? ChartNode => TransitionNode as BehaviorNode;

	public void BindIdentity(string instanceId, BehaviorNode chartNode)
	{
		InstanceId = instanceId;
		CurrentNode = chartNode;
		Name = BehaviorChartGraphNaming.BehaviorNode(instanceId);
		Title = BehaviorDataKeys.GetBehaviorShortName(chartNode.FullTypeName);
	}

	public void WireInitialBehaviorControl(ButtonGroup buttonGroup, string instanceId)
	{
		SetAsInitialButton.SetMeta(BehaviorGraphNodeLookup.InitialBehaviorInstanceIdMeta, instanceId);
		SetAsInitialButton.ButtonGroup = buttonGroup;
	}

	public virtual void RefreshFromChart(string instanceId, IBTChart? chart, IBTData? data)
	{
		if (ChartNode == null)
		{
			return;
		}

		Title = BehaviorDataKeys.GetBehaviorShortName(ChartNode.FullTypeName);
		SetAsInitialButton.SetPressedNoSignal(chart?.InitialBehaviorInstanceId == instanceId);
		ApplyProcessOrderPicker();
		ApplyTransitionOutputPorts(BehaviorChartTransitionNames.ForBehavior(data, ChartNode));
		AfterRefreshFromChart?.Invoke(data);
	}

	public override void _ExitTree() => _uiBindings.Clear();

	public override void _Ready() =>
		_uiBindings.BindOptionButton(ProcessOrderPicker, OnProcessOrderSelected);

	/// <summary>
	/// Applies one-off process-order defaults when direct-child count crosses 0↔1.
	/// Call after chart child topology has already been mutated.
	/// </summary>
	public bool ApplyAutomaticProcessOrderForChildCount(int previousChildCount)
	{
		if (ChartNode == null)
		{
			return false;
		}

		int childCount = ChartNode.ChildBehaviorInstanceIds?.Length ?? 0;
		if (previousChildCount == 0 && childCount == 1)
		{
			if (ChartNode.ProcessOrder == BehaviorProcessOrder.NoChildProcess
				&& ProcessOrderWhenChildren != BehaviorProcessOrder.NoChildProcess)
			{
				return TrySetProcessOrder(ProcessOrderWhenChildren);
			}
		}
		else if (previousChildCount > 0 && childCount == 0)
		{
			return TrySetProcessOrder(BehaviorProcessOrder.NoChildProcess);
		}

		return false;
	}

	protected void ApplyProcessOrderPicker()
	{
		_suppressProcessOrderChanged = true;
		int selectedIndex = ProcessOrderPicker.GetItemIndex((int)ChartNode!.ProcessOrder);
		if (selectedIndex >= 0)
		{
			ProcessOrderPicker.Select(selectedIndex);
		}

		_suppressProcessOrderChanged = false;
	}

	protected virtual void CommitProcessOrderMutation() => RequestChartMutation?.Invoke();

	protected bool TrySetProcessOrder(BehaviorProcessOrder processOrder)
	{
		if (ChartNode!.ProcessOrder == processOrder)
		{
			return false;
		}

		ChartNode.ProcessOrder = processOrder;
		ApplyProcessOrderPicker();
		return true;
	}

	private void OnProcessOrderSelected(long index)
	{
		if (_suppressProcessOrderChanged)
		{
			return;
		}

		if (TrySetProcessOrder((BehaviorProcessOrder)ProcessOrderPicker.GetItemId((int)index)))
		{
			CommitProcessOrderMutation();
		}
	}
}

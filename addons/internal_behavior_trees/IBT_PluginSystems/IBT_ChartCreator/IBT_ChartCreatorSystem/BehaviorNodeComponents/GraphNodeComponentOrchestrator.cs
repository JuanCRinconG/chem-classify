#if TOOLS
#nullable enable
using Godot;
using IBTSystem;

namespace IBTPlugin;

/// <summary>
/// Authoring-only coordinator for graph node capability rows.
/// Lives as a plain child Node on authoring graph scenes — not on IBTTransitionNode.
/// </summary>
[Tool]
public partial class GraphNodeComponentOrchestrator : Node
{
	[Export]
	public IBTTransitionNode GraphNode = null!;

	[Export]
	public Godot.Collections.Array<Node> Components { get; set; } = [];

	public override void _EnterTree()
	{
		BindComponents();
		GraphNode.AfterRefreshFromChart = RefreshComponents;
	}

	public override void _ExitTree()
	{
		if (GraphNode?.AfterRefreshFromChart == RefreshComponents)
		{
			GraphNode.AfterRefreshFromChart = null;
		}
	}

	private void BindComponents()
	{
		foreach (Node componentNode in Components)
		{
			if (componentNode is IBTNodeComponent component)
			{
				component.BindGraphNode(GraphNode);
			}
		}
	}

	private void RefreshComponents(IBTData? data)
	{
		foreach (Node componentNode in Components)
		{
			if (componentNode is IBTNodeComponent component)
			{
				component.RefreshFromChart(data);
			}
		}
	}
}
#endif

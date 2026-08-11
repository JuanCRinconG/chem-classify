#nullable enable

namespace IBTPlugin;

/// <summary>
/// Authoring capability row bound to a chart graph node via orchestrator injection.
/// </summary>
public interface IBTNodeComponent
{
	void BindGraphNode(IBTTransitionNode graphNode);

	void RefreshFromChart(IBTData? data);
}

using System;

public static class UISwapService
{
    /// <summary>
    /// Utility for swapping to a different scene
    /// </summary>
    /// <param name="Current"></param>
    /// <param name="Scene"></param>
    public static void SwapScene(Control Current, PackedScene Scene)
    {
        Current.Visible = false;
        var NextScene = Scene.Instantiate();
        MainAppCore.Current.UIContainerSpace.AddChild(NextScene);
        Current.QueueFree();
    }

    public static void SwapScenePath(Control current, string scenePath, Action<Node> configure = null)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            GD.PrintErr("UISwapService: NextScenePath is null or empty!");
            return;
        }

        // Load the resource on-demand at runtime
        var packedScene = GD.Load<PackedScene>(scenePath);
        var nextScene = packedScene.Instantiate();
        configure?.Invoke(nextScene);

        current.Visible = false;
        MainAppCore.Current.UIContainerSpace.AddChild(nextScene);
        current.QueueFree();
    }
}
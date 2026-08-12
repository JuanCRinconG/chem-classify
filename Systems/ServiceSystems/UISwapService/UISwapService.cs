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
        MainUISystem.Current.UIContainerSpace.AddChild(NextScene);
        Current.QueueFree();
    }

    public static void SwapScenePath(Control current, string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            GD.PrintErr("UISwapService: NextScenePath is null or empty!");
            return;
        }

        current.Visible = false;

        // Load the resource on-demand at runtime
        var packedScene = GD.Load<PackedScene>(scenePath);
        var nextScene = packedScene.Instantiate();

        MainUISystem.Current.UIContainerSpace.AddChild(nextScene);
        current.QueueFree();
    }
}
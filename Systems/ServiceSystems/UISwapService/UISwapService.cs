public class UISwapService
{
    /// <summary>
    /// Utility for swapping to a different scene
    /// </summary>
    /// <param name="Current"></param>
    /// <param name="Scene"></param>
    public void SwapScene(Control Current, PackedScene Scene)
    {
        Current.Visible = false;
        var NextScene = Scene.Instantiate();
        MainUISystem.CurrentInstance.UIContainerSpace.AddChild(NextScene);
        Current.QueueFree();
    }
}
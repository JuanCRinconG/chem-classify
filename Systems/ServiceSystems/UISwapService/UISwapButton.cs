public partial class UISwapButton : Button
{
    private UIBindings UIBinding = new();

    [Export]
    public Control CurrentSceneRoot;

     [Export(PropertyHint.File, "*.tscn,*.scn")]
    public string NextScenePath;

    public override void _EnterTree()
    {
        UIBinding.BindButton(this, OnSwapScenePressed);
    }

    public override void _ExitTree()
    {
        UIBinding.Clear();
    }

    public void OnSwapScenePressed()
    {
        UISwapService.SwapScenePath(CurrentSceneRoot, NextScenePath);
    }
}
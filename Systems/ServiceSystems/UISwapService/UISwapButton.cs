public partial class UISwapButton : Button
{
    private UIBindings UIBinding = new();

    [Export]
    public Control CurrentSceneRoot;

    [Export]
    public PackedScene NextScene;

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
        UISwapService.SwapScene(CurrentSceneRoot, NextScene);
    }
}
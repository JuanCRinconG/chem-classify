using Godot;

public partial class UISwapButton : Button
{

    private UIBindings UIBinding = new();

    [Export]
    public Control CurrentSceneRoot;

    [Export]
    public DisplayName NextDisplay;

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
        AppManager.Current.Board.Reset();
        AppManager.Current.SwapTo(CurrentSceneRoot, NextDisplay);
    }
}
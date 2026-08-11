public partial class CloseProgramButton : Button
{
    private UIBindings UIBinding = new();

    public override void _EnterTree()
    {
        UIBinding.BindButton(this, OnCloseProgramPressed);
    }

    public override void _ExitTree()
    {
        UIBinding.Clear();
    }


    public void OnCloseProgramPressed()
    {
        GetTree().Quit();
    }
}

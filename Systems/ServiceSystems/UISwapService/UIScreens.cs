public partial class UIScreens : Node
{
    public static UIScreens Current;

    [Export(PropertyHint.File, "*.tscn,*.scn")]
    public string TwoFA;

    [Export(PropertyHint.File, "*.tscn,*.scn")]
    public string MainApp;

    public override void _EnterTree()
    {
        Current = this;
    }
}
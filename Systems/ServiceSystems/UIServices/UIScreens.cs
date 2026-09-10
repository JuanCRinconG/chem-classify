public partial class UIScreens : Node
{
    private const string Format = "*.tscn,*.scn";
    public static UIScreens Current;

    [Export(PropertyHint.File, Format)]
    public string TwoFA;

    [Export(PropertyHint.File, Format)]
    public string MainApp;

    [Export(PropertyHint.File, Format)]
    public string LoginScreen;

    [Export(PropertyHint.File, Format)]
    public string CreateAccountScreen;

    public override void _EnterTree()
    {
        Current = this;
    }
}

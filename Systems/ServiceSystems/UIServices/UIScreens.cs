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

    [Export(PropertyHint.File, Format)]
    public string PdfRegisterScreen;

    [Export(PropertyHint.File, Format)]
    public string SdsLibraryScreen;

    [Export(PropertyHint.File, Format)]
    public string SdsDetailScreen;

    [Export(PropertyHint.File, Format)]
    public string QrLookupScreen;

    [Export(PropertyHint.File, Format)]
    public string ExpirationMonitorScreen;

    public override void _EnterTree()
    {
        Current = this;
    }
}

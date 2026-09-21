
[GlobalClass]
public partial class ErrorData : Resource
{
    [Export]
    public Vector2I OffsetFromCaster = Vector2I.Zero;

    [Export]
    public Texture2D WarnTexture;

    [Export]
    public string WarnText;

    public void Show(string ErrorMessage, Control Source)
    {
        WarnText = ErrorMessage;
        Services.Error.Current.CastErrorMessage(Source, this);
    }
}
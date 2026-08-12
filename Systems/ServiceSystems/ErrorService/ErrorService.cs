public partial class ErrorService : PopupPanel
{
	public static ErrorService Current;

	[Export]
	public TextureRect WarnIcon;
	
	[Export]
	public RichTextLabel WarnTextSpace;

	public override void _EnterTree()
	{
		Current = this;
		this.Visible = false;
	}

	public override void _ExitTree()
	{
		Current = null;
	}

	public void CastErrorMessage(Control Caster, ErrorData Error)
	{
		WarnIcon.Texture = Error.WarnTexture;
		WarnTextSpace.Text = Error.WarnText;
		this.Visible = true;
	}
}
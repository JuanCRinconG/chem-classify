public partial class ErrorService : PopupPanel
{
	public static ErrorService CurrentInstance;

	[Export]
	public TextureRect WarnIcon;
	
	[Export]
	public RichTextLabel WarnTextSpace;

	public override void _EnterTree()
	{
		CurrentInstance = this;
		this.Visible = false;
	}

	public override void _ExitTree()
	{
		CurrentInstance = null;
	}

	public void CastErrorMessage(Control Caster, ErrorData Error)
	{
		WarnIcon.Texture = Error.WarnTexture;
		WarnTextSpace.Text = Error.WarnText;
		this.Visible = true;
	}
}
public partial class ReusableError : PopupPanel
{
	public static ReusableError CurrentInstance;

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

	public void CastErrorMessage(ReusableErrorData ErrorData)
	{
		WarnIcon.Texture = ErrorData.WarnTexture;
		WarnTextSpace.Text = ErrorData.WarnText;
		this.Visible = true;
	}
}

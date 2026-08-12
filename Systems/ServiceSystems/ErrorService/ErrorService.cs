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
		Visible = false;
	}

	public void CastErrorMessage(Control Caster, ErrorData Error)
	{
		WarnIcon.Texture = Error.WarnTexture;
		WarnTextSpace.Text = Error.WarnText;
		ResetSize();
		Vector2I popupSize = Size;
		Rect2 CasterRect = Caster.GetGlobalRect();
		Vector2 anchor = new (CasterRect.Position.X, CasterRect.Position.Y);
		anchor += new Vector2(Error.OffsetFromCaster.X, Error.OffsetFromCaster.Y);
		Transform2D toScreen = Caster.GetViewport().GetScreenTransform();
		Vector2 screenPos = toScreen * anchor;
		Vector2I popupPos = (Vector2I)screenPos.Round();
		Vector2 visible = Caster.GetViewport().GetVisibleRect().Size;
		popupPos.X = Mathf.Clamp(popupPos.X, 0, (int)visible.X - popupSize.X);
		popupPos.Y = Mathf.Clamp(popupPos.Y, 0, (int)visible.Y - popupSize.Y);
		Popup(new Rect2I(popupPos, popupSize));
	}
}
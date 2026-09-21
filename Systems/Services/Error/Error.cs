namespace Services;

public partial class Error : PopupPanel
{
	public static Error Current;

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

		Vector2 popupSize = Size;
		Rect2 casterGlobalRect = Caster.GetGlobalRect();
		Vector2 targetPos = casterGlobalRect.GetCenter() - (popupSize * 0.5f);
		targetPos += Error.OffsetFromCaster;

		Rect2 viewportRect = Caster.GetViewport().GetVisibleRect();
		
		float minX = viewportRect.Position.X;
		float minY = viewportRect.Position.Y;
		float maxX = Mathf.Max(minX, viewportRect.End.X - popupSize.X);
		float maxY = Mathf.Max(minY, viewportRect.End.Y - popupSize.Y);

		Vector2I finalPos = (Vector2I)new Vector2(
			Mathf.Clamp(targetPos.X, minX, maxX),
			Mathf.Clamp(targetPos.Y, minY, maxY)
		).Round();

		Position = finalPos;
		Popup();
	}
}
[GlobalClass]
public partial class MailNode : Node
{
    [Export]
    public MailData MailMessage;

    public override void _EnterTree()
    {
        if (MailMessage == null)
        {
            GD.PrintErr("MailMessage is null");
            return;
        }
        _ = MailService.SendMail(MailMessage);
    }
}

[GlobalClass]
public partial class MailNode : Node
{
    [Export]
    public MailData MailMessage;

    [Export]
    public string MailReceiver;

    public override void _EnterTree()
    {
        if (MailMessage == null)
        {
            GD.PrintErr("MailMessage is null");
            return;
        }
        if (MailMessage.VerifyMail())
        {
            _ = MailService.SendMail(MailMessage, MailReceiver);
        }    
    }
}

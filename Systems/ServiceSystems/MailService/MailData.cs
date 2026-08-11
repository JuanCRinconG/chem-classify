[GlobalClass]
public partial class MailData : Resource
{
    [Export]
    public string MailReceiver;

    [Export]
    public string MailSubject;

    [Export]
    public string MailBody;

    public bool VerifyMail ()
    {
        bool result = NullChecker.GroupNullChecker(
            (MailReceiver, nameof(MailReceiver)),
            (MailSubject, nameof(MailSubject)),
            (MailBody, nameof(MailBody)));

        

        return result;
    }
}
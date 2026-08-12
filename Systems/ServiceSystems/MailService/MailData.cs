public class MailData
{
    public string Receiver;

    public string Subject;

    public string Body;

    public bool VerifyMail()
    {
        bool result = NullChecker.GroupNullChecker
        (
            (Receiver, nameof(Receiver)),
            (Subject, nameof(Subject)),
            (Body, nameof(Body))
        );
        result = LoginValidation.Email(Receiver) is string;
        return result;
    }

    public async void SendMail()
    {
        await MailService.SendMail(this);
    }
}
public class MailData
{
    public string Receiver;

    public string Subject;

    public string Body;

    public bool Verify()
    {
        bool result = NullCheck.Group
        (
            (Receiver, nameof(Receiver)),
            (Subject, nameof(Subject)),
            (Body, nameof(Body))
        );
        if (!result)
        {
            return result;
        }
        return !(LoginValidation.Email(Receiver) is string);
    }

    public async void Send()
    {
        await Services.Mail.SendMail(this);
    }
}
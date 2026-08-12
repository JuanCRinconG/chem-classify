using MimeKit;
using System.Threading.Tasks;

public static class MailService
{
    public static string host = System.Environment.GetEnvironmentVariable("SMTP_HOST");
    public static int port = int.Parse(System.Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
    public static string user = System.Environment.GetEnvironmentVariable("SMTP_USER");
    public static string pass = System.Environment.GetEnvironmentVariable("SMTP_PASS");
    public static string from = System.Environment.GetEnvironmentVariable("SMTP_FROM");

    public static async Task SendMail(MailData SendableMail, string MailReceiver)
    {
        var mailMessage = new MimeMessage();
        mailMessage.From.Add(MailboxAddress.Parse(from));
        mailMessage.To.Add(MailboxAddress.Parse(MailReceiver));
        mailMessage.Subject = SendableMail.MailSubject;
        mailMessage.Body = new TextPart("plain") { Text = SendableMail.MailBody};

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(user, pass);
        await client.SendAsync(mailMessage);
        await client.DisconnectAsync(true);

        GD.Print("Mail sent successfully");
    }
}
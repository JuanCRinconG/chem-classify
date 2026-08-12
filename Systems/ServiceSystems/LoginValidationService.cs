using System;
using System.Net.Mail;

public static class LoginValidationService
{
    public const string AllowedDomain = "ucundinamarca.edu.co";

    public const string SuccessFlag = "LoginValidationServiceValid";
    public static string ValidateEmail(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
        {
            return "The email field is empty";
        }

        try
        {
            var mail = new MailAddress(input);
            if(mail.Host.Equals(AllowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return SuccessFlag;
            }
        }
        catch (FormatException)
        {
            //todo: add a case for when the input string is not a domain
            return "The email does not belong to the allowed domain";
        }
        return "The email is invalid"; 
    }

    public static string ValidatePassword(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
        {
            return "The password field is empty";
        }
        return SuccessFlag;
    }

}
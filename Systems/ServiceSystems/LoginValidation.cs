using System;
using System.Net.Mail;

public static class LoginValidation
{
    public const string AllowedDomain = "ucundinamarca.edu.co";

    public const string SuccessFlag = "LoginValidationServiceValid";

    public const int MinPasswordLength = 6; // Firebase Auth minimum

    public static bool Invalid(string result) => result != SuccessFlag;

    public static string Email(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
        {
            return "The email field is empty";
        }

        try
        {
            var mail = new MailAddress(input);
            
            if (mail.Host.Equals(AllowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return SuccessFlag;
            }
            else
            {
                return "The email does not belong to the allowed domain";
            }
        }
        catch (FormatException)
        {
            return "The email format is invalid"; 
        } 
    }

    public static string Password(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
        {
            return "The password field is empty";
        }
        return SuccessFlag;
    }

   
    public static string PasswordForCreate(string input)
    {
        string baseResult = Password(input);
        if (baseResult != SuccessFlag)
            return baseResult;
        if (input.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters";
        return SuccessFlag;
    }
    public static string PasswordConfirmation(string password, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(confirmation))
            return "Please confirm your password";
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            return "Passwords do not match";
        return SuccessFlag;
    }
}
using System;
using System.Linq;
using System.Net.Mail;

namespace ProcSense.VR.Login
{
    public static class LoginInputValidation
    {
        public const int EmailLimit = 254;
        public const int PasswordLimit = 128;

        public static string EmailError(string value)
        {
            string address = (value ?? "").Trim();
            if (address.Length == 0) return "Enter your email address.";
            if (address.Length > EmailLimit) return "Email must be 254 characters or fewer.";
            if (address.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) return "Enter a valid email address.";
            try
            {
                var parsed = new MailAddress(address);
                if (parsed.Address != address || parsed.User.Length > 64 || parsed.User.StartsWith(".") || parsed.User.EndsWith(".") || parsed.User.Contains(".."))
                    return "Enter a valid email address.";
                var labels = parsed.Host.Split('.');
                if (labels.Length < 2 || labels.Any(label => label.Length == 0 || label.Length > 63 || label.StartsWith("-") || label.EndsWith("-") || label.Any(c => !char.IsLetterOrDigit(c) && c != '-')))
                    return "Enter a valid email address.";
            }
            catch (FormatException) { return "Enter a valid email address."; }
            return "";
        }

        public static string PasswordError(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Enter your password.";
            if (value.Length > PasswordLimit) return "Password must be 128 characters or fewer.";
            if (value.Any(char.IsControl)) return "Password cannot contain control characters.";
            // Login does not impose signup strength rules or silently trim a password.
            return "";
        }
    }
}

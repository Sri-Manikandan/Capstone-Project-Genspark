using System.Text.RegularExpressions;
using EMSModelLibrary.Exceptions;

namespace EMSBLLLibrary.Helpers
{
    public static class InputValidator
    {
        private static readonly Regex PhoneRegex = new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled);
        private static readonly Regex SpecialCharRegex = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

        public static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ValidationException("Name is required.");
            if (name.Trim().Length < 2)
                throw new ValidationException("Name must be at least 2 characters.");
            if (name.Length > 100)
                throw new ValidationException("Name must not exceed 100 characters.");
        }

        public static void ValidatePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ValidationException("Phone number is required.");
            if (!PhoneRegex.IsMatch(phone.Trim()))
                throw new ValidationException("Phone must be 7–15 digits and may start with '+'.");
        }

        public static void ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ValidationException("Email is required.");
            try
            {
                var addr = new System.Net.Mail.MailAddress(email.Trim());
                if (addr.Address != email.Trim())
                    throw new ValidationException("Invalid email format.");
            }
            catch (FormatException)
            {
                throw new ValidationException("Invalid email format.");
            }
        }

        public static void ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ValidationException("Password is required.");
            if (password.Length < 8)
                throw new ValidationException("Password must be at least 8 characters.");
            if (!password.Any(char.IsUpper))
                throw new ValidationException("Password must contain at least one uppercase letter.");
            if (!password.Any(char.IsLower))
                throw new ValidationException("Password must contain at least one lowercase letter.");
            if (!password.Any(char.IsDigit))
                throw new ValidationException("Password must contain at least one digit.");
            if (!SpecialCharRegex.IsMatch(password))
                throw new ValidationException("Password must contain at least one special character.");
        }

        public static void ValidateRequiredString(string fieldName, string value, int maxLength = 500)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ValidationException($"{fieldName} is required.");
            if (value.Trim().Length > maxLength)
                throw new ValidationException($"{fieldName} must not exceed {maxLength} characters.");
        }

        public static void ValidateUrl(string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ValidationException($"{fieldName} is required.");
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ValidationException($"{fieldName} must be a valid HTTP or HTTPS URL.");
        }

        public static void ValidatePositiveInt(string fieldName, int value)
        {
            if (value <= 0)
                throw new ValidationException($"{fieldName} must be greater than zero.");
        }
    }
}

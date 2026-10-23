namespace Situ.Domain.Identity;

/// <summary>Password rule for registration and changes: 8 characters, with an upper-case letter, a lower-case letter and a digit.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsValid(string? password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= MinimumLength
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit);
}

using System.Text.RegularExpressions;

namespace Situ.Domain.Common;

/// <summary>Normalized (trimmed, lower-case) e-mail address.</summary>
public sealed record Email
{
    private static readonly Regex Pattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string? input)
    {
        if (!TryCreate(input, out var email))
        {
            throw new DomainException("email.invalid", "The e-mail address is not valid.");
        }

        return email;
    }

    public static bool TryCreate(string? input, out Email email)
    {
        email = null!;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length > 254 || !Pattern.IsMatch(normalized))
        {
            return false;
        }

        email = new Email(normalized);
        return true;
    }

    public override string ToString() => Value;
}

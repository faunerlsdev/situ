using System.Text.RegularExpressions;

namespace Situ.Domain.Common;

/// <summary>A phone number stored as digits, with an optional leading +.</summary>
public sealed record PhoneNumber
{
    private static readonly Regex Separators = new(@"[\s().\-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Pattern = new(@"^\+?\d{7,15}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private PhoneNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PhoneNumber Create(string? input)
    {
        if (!TryCreate(input, out var phone))
        {
            throw new DomainException("phone.invalid", "The phone number is not valid.");
        }

        return phone;
    }

    public static bool TryCreate(string? input, out PhoneNumber phone)
    {
        phone = null!;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var compact = Separators.Replace(input.Trim(), "");
        if (compact.StartsWith("00", StringComparison.Ordinal))
        {
            compact = "+" + compact[2..];
        }

        if (!Pattern.IsMatch(compact))
        {
            return false;
        }

        phone = new PhoneNumber(compact);
        return true;
    }

    public override string ToString() => Value;
}

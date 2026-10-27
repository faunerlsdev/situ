using System.Text.RegularExpressions;

namespace Situ.Domain.Common;

/// <summary>
/// A phone number in E.164 (for example +573001234567). Uniqueness is not required:
/// two accounts may share a number, and the API never says whether one is already registered.
/// </summary>
public sealed record PhoneNumber
{
    private static readonly Regex Separators = new(@"[\s().\-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Pattern = new(@"^\+[1-9]\d{7,14}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    /// <summary>Shows the country code and the last four digits. The full number stays out of the panel.</summary>
    public string Mask()
    {
        var digits = Value[1..];
        var country = digits.StartsWith("57", StringComparison.Ordinal) && digits.Length > 6
            ? "57"
            : digits.Length > 10
                ? digits[..(digits.Length - 10)]
                : digits[..Math.Min(2, digits.Length)];
        var last = digits.Length >= 4 ? digits[^4..] : digits;
        return $"+{country} ••• ••• {last}";
    }

    public override string ToString() => Value;
}

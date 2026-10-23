using System.Text.RegularExpressions;

namespace Situ.Domain.Common;

/// <summary>Unique public handle: 3-30 chars, lower-case letters, digits, '_' and '.'. Used to find and invite friends.</summary>
public sealed record UserName
{
    private static readonly Regex Pattern = new(
        @"^[a-z0-9][a-z0-9_.]{2,29}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private UserName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static UserName Create(string? input)
    {
        if (!TryCreate(input, out var userName))
        {
            throw new DomainException(
                "username.invalid",
                "The user name must have 3 to 30 characters: lower-case letters, digits, '_' or '.'.");
        }

        return userName;
    }

    public static bool TryCreate(string? input, out UserName userName)
    {
        userName = null!;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim().ToLowerInvariant();
        if (!Pattern.IsMatch(normalized))
        {
            return false;
        }

        userName = new UserName(normalized);
        return true;
    }

    public override string ToString() => Value;
}

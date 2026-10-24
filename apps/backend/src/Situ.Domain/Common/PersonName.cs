using System.Text.RegularExpressions;

namespace Situ.Domain.Common;

/// <summary>A person's given names or surnames: letters, spaces, apostrophes and hyphens.</summary>
public sealed record PersonName
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Pattern = new(
        @"^[\p{L}][\p{L} '\-]*[\p{L}]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private PersonName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PersonName Create(string? input)
    {
        if (!TryCreate(input, out var name))
        {
            throw new DomainException("person_name.invalid", "The name is not valid.");
        }

        return name;
    }

    public static bool TryCreate(string? input, out PersonName name)
    {
        name = null!;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = Whitespace.Replace(input.Trim(), " ");
        if (normalized.Length is < 2 or > 80 || !Pattern.IsMatch(normalized))
        {
            return false;
        }

        name = new PersonName(normalized);
        return true;
    }

    public override string ToString() => Value;
}

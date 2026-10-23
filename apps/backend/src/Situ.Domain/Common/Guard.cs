namespace Situ.Domain.Common;

/// <summary>Small helpers to enforce invariants inside entities.</summary>
public static class Guard
{
    public static string NotEmpty(string? value, string name, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name}.required", $"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name}.too_long", $"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{name}.too_long", $"{name} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException($"{name}.required", $"{name} is required.");
        }

        return value;
    }

    public static int InRange(int value, int min, int max, string name)
    {
        if (value < min || value > max)
        {
            throw new DomainException($"{name}.out_of_range", $"{name} must be between {min} and {max}.");
        }

        return value;
    }

    public static double InRange(double value, double min, double max, string name)
    {
        if (double.IsNaN(value) || value < min || value > max)
        {
            throw new DomainException($"{name}.out_of_range", $"{name} must be between {min} and {max}.");
        }

        return value;
    }

    public static void That(bool condition, string code, string message)
    {
        if (!condition)
        {
            throw new DomainException(code, message);
        }
    }
}

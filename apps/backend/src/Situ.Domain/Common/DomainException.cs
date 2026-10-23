namespace Situ.Domain.Common;

/// <summary>
/// A business rule was violated. <see cref="Code"/> is a stable machine-readable identifier
/// (e.g. "user.below_minimum_age") that the API maps to a problem response.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

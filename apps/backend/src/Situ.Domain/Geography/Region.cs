using Situ.Domain.Common;

namespace Situ.Domain.Geography;

/// <summary>Intermediate administrative level (departamento, estado, provincia) selected at registration.</summary>
public sealed class Region : AuditableEntity
{
    private Region()
    {
        Name = null!;
    }

    public Guid CountryId { get; private set; }
    public string Name { get; private set; }

    /// <summary>Official code (e.g. DANE code for Colombian departments).</summary>
    public string? Code { get; private set; }

    public bool IsEnabled { get; private set; }

    public static Region Create(Guid countryId, string name, string? code = null) => new()
    {
        CountryId = Guard.NotEmpty(countryId, nameof(countryId)),
        Name = Guard.NotEmpty(name, nameof(name), 100),
        Code = Guard.Optional(code, nameof(code), 10),
        IsEnabled = true,
    };

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;
}

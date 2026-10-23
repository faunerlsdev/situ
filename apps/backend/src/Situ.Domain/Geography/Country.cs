using Situ.Domain.Common;

namespace Situ.Domain.Geography;

public enum DistanceUnit
{
    Kilometers,
    Miles,
}

/// <summary>Country catalog; only enabled countries are offered at registration (RF-GLO-01, RF-GLO-02, RF-AUT-03).</summary>
public sealed class Country : AggregateRoot
{
    private Country()
    {
        IsoCode = null!;
        Name = null!;
        RegionLabel = null!;
        DefaultLocale = null!;
        DefaultCurrency = null!;
        DefaultTimeZone = null!;
    }

    /// <summary>ISO 3166-1 alpha-2, upper case (e.g. "CO").</summary>
    public string IsoCode { get; private set; }

    public string Name { get; private set; }

    /// <summary>Name of the intermediate level for this country: "Departamento", "Estado", "Provincia"...</summary>
    public string RegionLabel { get; private set; }

    public string DefaultLocale { get; private set; }
    public string DefaultCurrency { get; private set; }
    public string DefaultTimeZone { get; private set; }
    public DistanceUnit DistanceUnit { get; private set; }

    /// <summary>Minimum age to create an account in this country (parameter, 14 for Colombia).</summary>
    public int MinimumAge { get; private set; }

    public bool IsEnabled { get; private set; }

    public static Country Create(
        string isoCode,
        string name,
        string regionLabel,
        string defaultLocale,
        string defaultCurrency,
        string defaultTimeZone,
        DistanceUnit distanceUnit,
        int minimumAge,
        bool isEnabled = false)
    {
        var code = Guard.NotEmpty(isoCode, nameof(isoCode), 2).ToUpperInvariant();
        Guard.That(code.Length == 2, "country.invalid_iso_code", "ISO code must have 2 letters.");

        return new Country
        {
            IsoCode = code,
            Name = Guard.NotEmpty(name, nameof(name), 100),
            RegionLabel = Guard.NotEmpty(regionLabel, nameof(regionLabel), 50),
            DefaultLocale = Guard.NotEmpty(defaultLocale, nameof(defaultLocale), 15),
            DefaultCurrency = Guard.NotEmpty(defaultCurrency, nameof(defaultCurrency), 3).ToUpperInvariant(),
            DefaultTimeZone = Guard.NotEmpty(defaultTimeZone, nameof(defaultTimeZone), 64),
            DistanceUnit = distanceUnit,
            MinimumAge = Guard.InRange(minimumAge, 13, 18, nameof(minimumAge)),
            IsEnabled = isEnabled,
        };
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    /// <summary>Lets operations raise the minimum age (e.g. if a law passes) without a release.</summary>
    public void ChangeMinimumAge(int minimumAge) =>
        MinimumAge = Guard.InRange(minimumAge, 13, 18, nameof(minimumAge));
}

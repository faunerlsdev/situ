using Situ.Domain.Common;

namespace Situ.Domain.Legal;

public enum LegalDocumentType
{
    TermsOfUse,
    PrivacyPolicy,
    PublicProfileConsent,
    LocationSharingConsent,
    PersonalizedAdsConsent,
    GuardianNotice,
    AnonymityNotice,
}

/// <summary>
/// Link to an external legal text (Terms of Use, Data Processing Policy) per country and language,
/// managed from the admin panel (RF-MEN-02, RF-GLO-05). Each publication has a version that users accept.
/// </summary>
public sealed class LegalDocument : AuditableEntity
{
    private LegalDocument()
    {
        Locale = null!;
        Version = null!;
        Url = null!;
    }

    public LegalDocumentType Type { get; private set; }

    /// <summary>Null means "applies to every country".</summary>
    public Guid? CountryId { get; private set; }

    public string Locale { get; private set; }
    public string Version { get; private set; }
    public string Url { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    public static LegalDocument Publish(
        LegalDocumentType type, Guid? countryId, string locale, string version, string url, DateTimeOffset now)
    {
        Guard.That(
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
            "legal_document.invalid_url",
            "The legal document must be an absolute https URL.");

        var document = new LegalDocument
        {
            Type = type,
            CountryId = countryId,
            Locale = Guard.NotEmpty(locale, nameof(locale), 15),
            Version = Guard.NotEmpty(version, nameof(version), 30),
            Url = url.Trim(),
            IsActive = true,
            PublishedAt = now,
        };
        document.MarkCreated(now);
        return document;
    }

    public void Retire() => IsActive = false;
}

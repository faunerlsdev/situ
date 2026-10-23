using Situ.Domain.Common;

namespace Situ.Domain.Organizations;

public enum OrganizationType
{
    Business,
    Media,
    Government,
    Community,
}

public enum OrganizationStatus
{
    PendingVerification,
    Verified,
    Suspended,
}

/// <summary>Verified account for media, authorities and businesses (RF-ORG-01..04).</summary>
public sealed class Organization : AggregateRoot
{
    private Organization()
    {
        Name = null!;
        Slug = null!;
    }

    public string Name { get; private set; }
    public string Slug { get; private set; }
    public OrganizationType Type { get; private set; }
    public OrganizationStatus Status { get; private set; }
    public Guid? CityId { get; private set; }
    public string? Description { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? LogoKey { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }

    public bool IsVerified => Status == OrganizationStatus.Verified;

    public static Organization Create(string name, string slug, OrganizationType type, Guid? cityId, DateTimeOffset now)
    {
        var organization = new Organization
        {
            Name = Guard.NotEmpty(name, nameof(name), 100),
            Slug = Guard.NotEmpty(slug, nameof(slug), 60).ToLowerInvariant(),
            Type = type,
            CityId = cityId,
            Status = OrganizationStatus.PendingVerification,
        };

        organization.MarkCreated(now);
        return organization;
    }

    public void UpdateProfile(string name, string? description, string? websiteUrl, string? logoKey, DateTimeOffset now)
    {
        if (websiteUrl is not null)
        {
            Guard.That(
                Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "organization.invalid_website",
                "The website must be an https URL.");
        }

        Name = Guard.NotEmpty(name, nameof(name), 100);
        Description = Guard.Optional(description, nameof(description), 500);
        WebsiteUrl = websiteUrl;
        LogoKey = Guard.Optional(logoKey, nameof(logoKey), 500);
        MarkUpdated(now);
    }

    public void Verify(DateTimeOffset now)
    {
        Status = OrganizationStatus.Verified;
        VerifiedAt = now;
        MarkUpdated(now);
    }

    public void Suspend(DateTimeOffset now)
    {
        Status = OrganizationStatus.Suspended;
        MarkUpdated(now);
    }
}

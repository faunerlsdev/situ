using Situ.Domain.Common;

namespace Situ.Domain.Legal;

public enum ConsentMethod
{
    Registration,
    Settings,
    GuardianFlow,
    AdulthoodReacceptance,
    FirstUse,
}

/// <summary>
/// Evidence that a user accepted a legal text or gave a specific consent (Ley 1581 de 2012):
/// date, version, method and origin. Revoking a consent keeps the record and sets <see cref="RevokedAt"/>.
/// </summary>
public sealed class ConsentRecord : Entity
{
    private ConsentRecord()
    {
        Version = null!;
    }

    public Guid UserId { get; private set; }
    public LegalDocumentType DocumentType { get; private set; }
    public string Version { get; private set; }
    public Guid? CountryId { get; private set; }
    public ConsentMethod Method { get; private set; }
    public DateTimeOffset AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static ConsentRecord Accept(
        Guid userId,
        LegalDocumentType documentType,
        string version,
        Guid? countryId,
        ConsentMethod method,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        DocumentType = documentType,
        Version = Guard.NotEmpty(version, nameof(version), 30),
        CountryId = countryId,
        Method = method,
        IpAddress = Guard.Optional(ipAddress, nameof(ipAddress), 64),
        UserAgent = Guard.Optional(userAgent, nameof(userAgent), 512),
        AcceptedAt = now,
    };

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}

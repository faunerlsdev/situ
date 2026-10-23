using Situ.Domain.Common;

namespace Situ.Domain.Guardianship;

public enum GuardianConsentStatus
{
    Requested,
    EmailVerified,
    Authorized,
    Expired,
    Revoked,
    Declined,
}

public enum GuardianDocumentType
{
    CitizenshipCard,
    ForeignerCard,
    Passport,
    Other,
}

/// <summary>
/// Legal guardian authorization for a user under 18 (Ley 1581 de 2012, Decreto 1377 de 2013 art. 12; RF-MIN-01..03).
/// Flow: the minor only types the guardian's e-mail → the guardian opens the e-mailed link, which shows the minor's
/// information and the privacy notice → completes name, document number and e-mail → verifies the e-mail →
/// authorizes → SITU e-mails a receipt carrying an internal digital signature, kept as proof.
/// The guardian has a limited window (7 days) to answer.
/// </summary>
public sealed class GuardianConsent : AggregateRoot
{
    private GuardianConsent()
    {
        GuardianEmail = null!;
        TokenHash = null!;
        NoticeVersion = null!;
    }

    public Guid MinorUserId { get; private set; }

    /// <summary>E-mail typed by the minor; the link is sent here.</summary>
    public string GuardianEmail { get; private set; }

    public string TokenHash { get; private set; }

    /// <summary>Version of the privacy notice shown to the guardian.</summary>
    public string NoticeVersion { get; private set; }

    /// <summary>When the minor was heard (plain-language screen) before the request was sent (Decreto 1377 art. 12).</summary>
    public DateTimeOffset MinorHeardAt { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public GuardianConsentStatus Status { get; private set; }

    public string? GuardianFullName { get; private set; }
    public GuardianDocumentType? GuardianDocumentType { get; private set; }
    public string? GuardianDocumentNumber { get; private set; }

    /// <summary>E-mail the guardian typed in the form; it is the one that gets verified.</summary>
    public string? GuardianContactEmail { get; private set; }

    public DateTimeOffset? GuardianEmailVerifiedAt { get; private set; }
    public DateTimeOffset? AuthorizedAt { get; private set; }

    /// <summary>Internal digital signature (e.g. HMAC-SHA256 over the consent data) included in the receipt e-mail.</summary>
    public string? DigitalSignature { get; private set; }

    public DateTimeOffset? ReceiptSentAt { get; private set; }

    /// <summary>Specific authorization for live streaming (needed under 16).</summary>
    public bool LiveStreamingAuthorized { get; private set; }

    /// <summary>Specific authorization to share location with friends (additional purpose).</summary>
    public bool LocationSharingAuthorized { get; private set; }

    public string? AuthorizationIpAddress { get; private set; }
    public string? AuthorizationUserAgent { get; private set; }
    public DateTimeOffset? DeclinedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static GuardianConsent Request(
        Guid minorUserId,
        string guardianEmail,
        string tokenHash,
        string noticeVersion,
        DateTimeOffset minorHeardAt,
        DateTimeOffset now,
        TimeSpan? validFor = null)
    {
        var email = Email.Create(guardianEmail).Value;

        var consent = new GuardianConsent
        {
            MinorUserId = Guard.NotEmpty(minorUserId, nameof(minorUserId)),
            GuardianEmail = email,
            TokenHash = Guard.NotEmpty(tokenHash, nameof(tokenHash)),
            NoticeVersion = Guard.NotEmpty(noticeVersion, nameof(noticeVersion), 30),
            MinorHeardAt = minorHeardAt,
            RequestedAt = now,
            ExpiresAt = now.Add(validFor ?? MinorProtectionPolicy.DefaultGuardianResponseWindow),
            Status = GuardianConsentStatus.Requested,
        };

        consent.MarkCreated(now);
        consent.Raise(new GuardianConsentRequestedEvent(consent.Id, minorUserId, email));
        return consent;
    }

    public bool IsOpen(DateTimeOffset now) =>
        Status is GuardianConsentStatus.Requested or GuardianConsentStatus.EmailVerified && now < ExpiresAt;

    /// <summary>Step 1 in the link: the guardian completes the form. Any previous e-mail verification is discarded.</summary>
    public void SubmitGuardianData(
        string fullName,
        GuardianDocumentType documentType,
        string documentNumber,
        string contactEmail,
        DateTimeOffset now)
    {
        EnsureOpen(now);

        GuardianFullName = Guard.NotEmpty(fullName, nameof(fullName), 150);
        GuardianDocumentType = documentType;
        GuardianDocumentNumber = Guard.NotEmpty(documentNumber, nameof(documentNumber), 30);
        GuardianContactEmail = Email.Create(contactEmail).Value;
        GuardianEmailVerifiedAt = null;
        Status = GuardianConsentStatus.Requested;
    }

    /// <summary>Step 2: the guardian proved control of the e-mail they typed.</summary>
    public void VerifyGuardianEmail(DateTimeOffset now)
    {
        EnsureOpen(now);
        Guard.That(GuardianContactEmail is not null, "guardian_consent.data_missing", "Guardian data must be completed first.");

        GuardianEmailVerifiedAt = now;
        Status = GuardianConsentStatus.EmailVerified;
    }

    /// <summary>Step 3: the guardian authorizes. <paramref name="digitalSignature"/> is produced by the signature service.</summary>
    public void Authorize(
        string digitalSignature,
        bool liveStreamingAuthorized,
        bool locationSharingAuthorized,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now)
    {
        EnsureOpen(now);
        Guard.That(
            Status == GuardianConsentStatus.EmailVerified,
            "guardian_consent.email_not_verified",
            "The guardian e-mail must be verified before authorizing.");

        DigitalSignature = Guard.NotEmpty(digitalSignature, nameof(digitalSignature), 512);
        LiveStreamingAuthorized = liveStreamingAuthorized;
        LocationSharingAuthorized = locationSharingAuthorized;
        AuthorizationIpAddress = Guard.Optional(ipAddress, nameof(ipAddress), 64);
        AuthorizationUserAgent = Guard.Optional(userAgent, nameof(userAgent), 512);
        AuthorizedAt = now;
        Status = GuardianConsentStatus.Authorized;

        Raise(new GuardianConsentAuthorizedEvent(Id, MinorUserId));
    }

    public void MarkReceiptSent(DateTimeOffset now)
    {
        Guard.That(Status == GuardianConsentStatus.Authorized, "guardian_consent.not_authorized", "No receipt without an authorization.");
        ReceiptSentAt = now;
    }

    public void Decline(DateTimeOffset now)
    {
        EnsureOpen(now);
        DeclinedAt = now;
        Status = GuardianConsentStatus.Declined;
    }

    /// <summary>The guardian withdraws a previous authorization (right to revoke).</summary>
    public void Revoke(DateTimeOffset now)
    {
        Guard.That(Status == GuardianConsentStatus.Authorized, "guardian_consent.not_authorized", "Only an authorized consent can be revoked.");
        RevokedAt = now;
        Status = GuardianConsentStatus.Revoked;
        Raise(new GuardianConsentRevokedEvent(Id, MinorUserId));
    }

    /// <summary>Marks an unanswered request as expired once the response window has passed. Returns true if it changed.</summary>
    public bool ExpireIfDue(DateTimeOffset now)
    {
        if (Status is GuardianConsentStatus.Requested or GuardianConsentStatus.EmailVerified && now >= ExpiresAt)
        {
            Status = GuardianConsentStatus.Expired;
            return true;
        }

        return false;
    }

    private void EnsureOpen(DateTimeOffset now)
    {
        Guard.That(
            Status is GuardianConsentStatus.Requested or GuardianConsentStatus.EmailVerified,
            "guardian_consent.not_open",
            "This authorization request is no longer open.");
        Guard.That(now < ExpiresAt, "guardian_consent.expired", "This authorization request has expired.");
    }
}

public sealed record GuardianConsentRequestedEvent(Guid ConsentId, Guid MinorUserId, string GuardianEmail) : DomainEvent;

public sealed record GuardianConsentAuthorizedEvent(Guid ConsentId, Guid MinorUserId) : DomainEvent;

public sealed record GuardianConsentRevokedEvent(Guid ConsentId, Guid MinorUserId) : DomainEvent;

namespace Situ.Application.Common.Abstractions;

/// <summary>Produces the internal signature stored with a guardian authorization and printed on the receipt.</summary>
public interface IGuardianConsentSigner
{
    string Sign(GuardianConsentSignature input);
}

public sealed record GuardianConsentSignature(
    Guid ConsentId,
    Guid MinorUserId,
    string NoticeVersion,
    string GuardianFullName,
    string DocumentType,
    string DocumentNumber,
    string ContactEmail,
    bool LiveStreamingAuthorized,
    bool LocationSharingAuthorized,
    DateTimeOffset AuthorizedAt);

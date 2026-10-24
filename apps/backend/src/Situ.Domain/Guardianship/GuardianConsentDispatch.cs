using Situ.Domain.Common;

namespace Situ.Domain.Guardianship;

public enum GuardianDispatchKind
{
    Request,
    Resend,
    EmailChange,
}

/// <summary>One outbound guardian email, kept only to enforce the send limits.</summary>
public sealed class GuardianConsentDispatch : Entity
{
    private GuardianConsentDispatch()
    {
        DestinationEmail = null!;
    }

    public Guid MinorUserId { get; private set; }
    public string DestinationEmail { get; private set; }
    public string? IpAddress { get; private set; }
    public GuardianDispatchKind Kind { get; private set; }
    public DateTimeOffset SentAt { get; private set; }

    public static GuardianConsentDispatch Record(
        Guid minorUserId,
        string destinationEmail,
        string? ipAddress,
        GuardianDispatchKind kind,
        DateTimeOffset now) => new()
    {
        MinorUserId = Guard.NotEmpty(minorUserId, nameof(minorUserId)),
        DestinationEmail = global::Situ.Domain.Common.Email.Create(destinationEmail).Value,
        IpAddress = Guard.Optional(ipAddress, nameof(ipAddress), 64),
        Kind = kind,
        SentAt = now,
    };
}

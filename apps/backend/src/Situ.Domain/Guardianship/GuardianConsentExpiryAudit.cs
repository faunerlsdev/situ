using Situ.Domain.Common;

namespace Situ.Domain.Guardianship;

/// <summary>Proof that an unanswered request expired. Holds no name, email or document of the minor.</summary>
public sealed class GuardianConsentExpiryAudit : Entity
{
    public Guid ConsentId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public bool PersonalDataDeleted { get; private set; }

    public static GuardianConsentExpiryAudit Record(Guid consentId, bool personalDataDeleted, DateTimeOffset now) => new()
    {
        ConsentId = consentId,
        OccurredAt = now,
        PersonalDataDeleted = personalDataDeleted,
    };
}

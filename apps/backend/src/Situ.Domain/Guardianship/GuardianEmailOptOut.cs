using Situ.Domain.Common;

namespace Situ.Domain.Guardianship;

/// <summary>An address that asked not to receive further guardian requests. Stored normalized, never written to logs.</summary>
public sealed class GuardianEmailOptOut : AuditableEntity
{
    private GuardianEmailOptOut()
    {
        Email = null!;
    }

    public string Email { get; private set; }

    public static GuardianEmailOptOut Create(string email, DateTimeOffset now)
    {
        var row = new GuardianEmailOptOut { Email = global::Situ.Domain.Common.Email.Create(email).Value };
        row.MarkCreated(now);
        return row;
    }
}

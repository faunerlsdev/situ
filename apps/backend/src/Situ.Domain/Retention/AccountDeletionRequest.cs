using Situ.Domain.Common;

namespace Situ.Domain.Retention;

public enum AccountDeletionStatus
{
    Scheduled,
    Cancelled,
    Completed,
    BlockedByLegalHold,
}

/// <summary>Right to erasure (habeas data). Has a grace period during which the user can change their mind.</summary>
public sealed class AccountDeletionRequest : AggregateRoot
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(14);

    private AccountDeletionRequest()
    {
    }

    public Guid UserId { get; private set; }
    public AccountDeletionStatus Status { get; private set; }
    public DateTimeOffset ScheduledFor { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static AccountDeletionRequest Create(Guid userId, DateTimeOffset now)
    {
        var request = new AccountDeletionRequest
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Status = AccountDeletionStatus.Scheduled,
            ScheduledFor = now.Add(GracePeriod),
        };

        request.MarkCreated(now);
        return request;
    }

    public bool IsDue(DateTimeOffset now) => Status == AccountDeletionStatus.Scheduled && now >= ScheduledFor;

    public void Cancel(DateTimeOffset now)
    {
        Guard.That(Status == AccountDeletionStatus.Scheduled, "account_deletion.not_scheduled", "The request cannot be cancelled.");
        Status = AccountDeletionStatus.Cancelled;
        MarkUpdated(now);
    }

    public void BlockByLegalHold(DateTimeOffset now)
    {
        Status = AccountDeletionStatus.BlockedByLegalHold;
        MarkUpdated(now);
    }

    public void Complete(DateTimeOffset now)
    {
        Guard.That(IsDue(now), "account_deletion.not_due", "The grace period has not ended.");
        Status = AccountDeletionStatus.Completed;
        CompletedAt = now;
        MarkUpdated(now);
    }
}

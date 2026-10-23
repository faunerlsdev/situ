using Situ.Domain.Common;

namespace Situ.Domain.News;

/// <summary>
/// Reputation of a user as a source of reports (internal, 0-10). Anonymous reports also count for the real author,
/// so anonymity does not let someone abuse the system without consequences (RF-ANO-07).
/// </summary>
public sealed class UserReputation : AuditableEntity
{
    public const double Min = 0;
    public const double Max = 10;
    public const double Initial = 5;

    private UserReputation()
    {
    }

    public Guid UserId { get; private set; }
    public double Score { get; private set; }
    public int ConfirmedReports { get; private set; }
    public int RejectedReports { get; private set; }

    public static UserReputation CreateDefault(Guid userId, DateTimeOffset now)
    {
        var reputation = new UserReputation
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Score = Initial,
        };

        reputation.MarkCreated(now);
        return reputation;
    }

    public void RewardConfirmedReport(DateTimeOffset now)
    {
        ConfirmedReports++;
        Score = Math.Min(Max, Score + 0.2);
        MarkUpdated(now);
    }

    public void PenalizeRejectedReport(DateTimeOffset now)
    {
        RejectedReports++;
        Score = Math.Max(Min, Score - 0.7);
        MarkUpdated(now);
    }
}

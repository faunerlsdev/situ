using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>One vote per user per report ("sigue ahí" / "ya no está"). Voting again replaces the previous vote.</summary>
public sealed class ReportConfirmation : AuditableEntity
{
    private ReportConfirmation()
    {
    }

    public Guid ReportId { get; private set; }
    public Guid UserId { get; private set; }
    public ConfirmationType Type { get; private set; }

    /// <summary>Kept for older rows. New votes check the distance and do not store the coordinate.</summary>
    public GeoPoint? VoterLocation { get; private set; }

    public static ReportConfirmation Create(Guid reportId, Guid userId, ConfirmationType type, GeoPoint? voterLocation, DateTimeOffset now)
    {
        var confirmation = new ReportConfirmation
        {
            ReportId = Guard.NotEmpty(reportId, nameof(reportId)),
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Type = type,
            VoterLocation = voterLocation,
        };

        confirmation.MarkCreated(now);
        return confirmation;
    }

    public void Change(ConfirmationType type, GeoPoint? voterLocation, DateTimeOffset now)
    {
        Type = type;
        VoterLocation = voterLocation;
        MarkUpdated(now);
    }
}

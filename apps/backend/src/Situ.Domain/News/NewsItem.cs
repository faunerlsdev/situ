using Situ.Domain.Common;

namespace Situ.Domain.News;

/// <summary>A report promoted to "important news" for a city. Appears in the news feed and triggers notifications.</summary>
public sealed class NewsItem : AuditableEntity
{
    private NewsItem()
    {
    }

    public Guid ReportId { get; private set; }
    public Guid? CityId { get; private set; }
    public Guid CategoryId { get; private set; }
    public double Score { get; private set; }
    public DateTimeOffset PromotedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsPinnedByStaff { get; private set; }
    public bool IsHidden { get; private set; }

    public static NewsItem Promote(Guid reportId, Guid categoryId, Guid? cityId, double score, DateTimeOffset reportExpiresAt, DateTimeOffset now)
    {
        var item = new NewsItem
        {
            ReportId = Guard.NotEmpty(reportId, nameof(reportId)),
            CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId)),
            CityId = cityId,
            Score = score,
            PromotedAt = now,
            ExpiresAt = reportExpiresAt,
        };

        item.MarkCreated(now);
        return item;
    }

    public bool IsVisible(DateTimeOffset now) => !IsHidden && now < ExpiresAt;

    public void UpdateScore(double score, DateTimeOffset now)
    {
        Score = score;
        MarkUpdated(now);
    }

    public void ExtendUntil(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (expiresAt > ExpiresAt)
        {
            ExpiresAt = expiresAt;
            MarkUpdated(now);
        }
    }

    public void PinByStaff(bool pinned, DateTimeOffset now)
    {
        IsPinnedByStaff = pinned;
        MarkUpdated(now);
    }

    public void Hide(DateTimeOffset now)
    {
        IsHidden = true;
        MarkUpdated(now);
    }
}

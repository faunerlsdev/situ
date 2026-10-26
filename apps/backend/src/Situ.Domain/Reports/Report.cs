using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>
/// A "chisme": something a user reports at a place. Lives on the map until <see cref="ExpiresAt"/> (category TTL),
/// can be confirmed ("sigue ahí" / "ya no está"), reacted to and commented.
/// </summary>
public sealed class Report : AggregateRoot
{
    private Report()
    {
        Description = string.Empty;
    }

    public Guid AuthorId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? CityId { get; private set; }
    public GeoPoint Location { get; private set; }
    public string Description { get; private set; }

    /// <summary>When true the public author is hidden (name, avatar, profile). The real author is still stored (RF-ANO-01).</summary>
    public bool IsAnonymous { get; private set; }

    /// <summary>
    /// Anonymous reports must not surface the author's avatar on the map, even when their profile is public (RF-UBI-11).
    /// The map client reads this flag later; the API already omits the avatar.
    /// </summary>
    public bool HideAuthorOnMap => IsAnonymous;

    public ContentVisibility Visibility { get; private set; }

    /// <summary>Author was under 18 when posting. Used to restrict visibility and moderation.</summary>
    public bool AuthorIsMinor { get; private set; }

    public ReportStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set when the report originates from a saved live stream (RF-LIV-07).</summary>
    public Guid? SourceLiveStreamId { get; private set; }

    public int StillThereCount { get; private set; }
    public int GoneNowCount { get; private set; }
    public int CommentCount { get; private set; }
    public int ReactionCount { get; private set; }
    public bool IsImportant { get; private set; }

    public bool IsVisible(DateTimeOffset now) => Status == ReportStatus.Active && now < ExpiresAt;

    public static Report Create(
        Guid authorId,
        Guid categoryId,
        Guid? cityId,
        GeoPoint location,
        string description,
        bool isAnonymous,
        bool authorIsMinor,
        TimeSpan lifetime,
        DateTimeOffset now,
        Guid? sourceLiveStreamId = null,
        ContentVisibility visibility = ContentVisibility.Public)
    {
        Guard.That(lifetime > TimeSpan.Zero, "report.invalid_lifetime", "Lifetime must be positive.");

        var report = new Report
        {
            AuthorId = Guard.NotEmpty(authorId, nameof(authorId)),
            CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId)),
            CityId = cityId,
            Location = location,
            Description = Guard.NotEmpty(description, nameof(description), 1000),
            IsAnonymous = isAnonymous,
            Visibility = visibility,
            AuthorIsMinor = authorIsMinor,
            Status = ReportStatus.Active,
            ExpiresAt = now.Add(lifetime),
            SourceLiveStreamId = sourceLiveStreamId,
        };

        report.MarkCreated(now);
        report.Raise(new ReportCreatedEvent(report.Id, categoryId, cityId, location, isAnonymous));
        return report;
    }

    public void UpdateDescription(string description, DateTimeOffset now)
    {
        EnsureActive();
        Description = Guard.NotEmpty(description, nameof(description), 1000);
        MarkUpdated(now);
        Raise(new ReportMapChanged(Id, ReportMapChanged.Updated));
    }

    /// <summary>A confirmation that the thing is still there extends the life of the report.</summary>
    public void RegisterConfirmation(ConfirmationType type, TimeSpan extension, DateTimeOffset now)
    {
        EnsureActive();

        if (type == ConfirmationType.StillThere)
        {
            StillThereCount++;
            ExpiresAt = ExpiresAt > now ? ExpiresAt.Add(extension) : now.Add(extension);
        }
        else
        {
            GoneNowCount++;
        }

        MarkUpdated(now);
        Raise(new ReportMapChanged(Id, ReportMapChanged.Updated));
    }

    /// <summary>
    /// Replaces a user's previous vote without counting it twice.
    /// The same vote again does not change the counters or the lifetime.
    /// </summary>
    public void ReplaceConfirmation(ConfirmationType? previous, ConfirmationType next, TimeSpan extension, DateTimeOffset now)
    {
        if (previous == next)
        {
            EnsureActive();
            return;
        }

        if (previous == ConfirmationType.StillThere && StillThereCount > 0)
        {
            StillThereCount--;
        }

        if (previous == ConfirmationType.GoneNow && GoneNowCount > 0)
        {
            GoneNowCount--;
        }

        RegisterConfirmation(next, extension, now);
    }

    /// <summary>Report is considered resolved when "gone" votes clearly outweigh "still there".</summary>
    public bool ShouldExpireByVotes(int minimumVotes = 3) =>
        GoneNowCount >= minimumVotes && GoneNowCount > StillThereCount * 2;

    public void AddComment() => CommentCount++;

    public void RemoveComment()
    {
        if (CommentCount > 0)
        {
            CommentCount--;
        }
    }

    public void SetReactionCount(int count) => ReactionCount = Guard.InRange(count, 0, int.MaxValue, nameof(count));

    public void MarkImportant(DateTimeOffset now)
    {
        if (IsImportant)
        {
            return;
        }

        IsImportant = true;
        MarkUpdated(now);
        Raise(new ReportBecameImportantEvent(Id, CategoryId, CityId, Location));
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status != ReportStatus.Active)
        {
            return;
        }

        Status = ReportStatus.Expired;
        MarkUpdated(now);
        Raise(new ReportMapChanged(Id, ReportMapChanged.Expired));
    }

    /// <summary>
    /// Same stored status as <see cref="Expire"/> (the report leaves the map) when "ya no está" wins.
    /// The zone event is resolved, so a client can tell a vote outcome from a TTL expiry.
    /// </summary>
    public void Resolve(DateTimeOffset now)
    {
        if (Status != ReportStatus.Active)
        {
            return;
        }

        Status = ReportStatus.Expired;
        MarkUpdated(now);
        Raise(new ReportMapChanged(Id, ReportMapChanged.Resolved));
    }

    public void SendToReview(DateTimeOffset now)
    {
        if (Status == ReportStatus.Active)
        {
            Status = ReportStatus.UnderReview;
            MarkUpdated(now);
            Raise(new ReportMapChanged(Id, ReportMapChanged.Updated));
        }
    }

    public void Restore(DateTimeOffset now)
    {
        Guard.That(Status == ReportStatus.UnderReview, "report.not_under_review", "Only reports under review can be restored.");
        Status = ReportStatus.Active;
        MarkUpdated(now);
        Raise(new ReportMapChanged(Id, ReportMapChanged.Updated));
    }

    public void Remove(DateTimeOffset now)
    {
        Status = ReportStatus.Removed;
        MarkUpdated(now);
        Raise(new ReportMapChanged(Id, ReportMapChanged.Removed));
    }

    private void EnsureActive() =>
        Guard.That(Status == ReportStatus.Active, "report.not_active", "The report is no longer active.");
}

public sealed record ReportCreatedEvent(Guid ReportId, Guid CategoryId, Guid? CityId, GeoPoint Location, bool IsAnonymous) : DomainEvent;

public sealed record ReportBecameImportantEvent(Guid ReportId, Guid CategoryId, Guid? CityId, GeoPoint Location) : DomainEvent;

/// <summary>
/// A map-relevant change. Carries the report id and the kind only.
/// The zone publisher never reads the description or the author from this event.
/// </summary>
public sealed record ReportMapChanged(Guid ReportId, string Kind) : DomainEvent
{
    public const string Updated = "ReportUpdated";
    public const string Resolved = "ReportResolved";
    public const string Expired = "ReportExpired";
    public const string Removed = "ReportRemoved";
}

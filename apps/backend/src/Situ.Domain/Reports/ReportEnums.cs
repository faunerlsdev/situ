namespace Situ.Domain.Reports;

public enum ReportStatus
{
    Active,
    Expired,
    Removed,
    UnderReview,
}

public enum MediaType
{
    Image,
    Video,
}

public enum MediaProcessingStatus
{
    Pending,
    Ready,
    Failed,
}

/// <summary>"Sigue ahí" / "Ya no está" vote on a report (RF-REP-06).</summary>
public enum ConfirmationType
{
    StillThere,
    GoneNow,
}

public enum ReactionType
{
    Like,
    Love,
    Haha,
    Wow,
    Sad,
    Angry,
}

/// <summary>Whether a category requires evidence when reporting (RF-CAT-03).</summary>
public enum MediaRequirement
{
    None,
    PhotoRequired,
    VideoRequired,
    PhotoOrVideoRequired,
}

public enum CommentStatus
{
    Visible,
    Hidden,
    Removed,
}

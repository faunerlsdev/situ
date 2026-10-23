namespace Situ.Domain.Common;

/// <summary>What a polymorphic reference (comment, reaction, complaint, legal hold...) points to.</summary>
public enum ContentTargetType
{
    Report,
    Comment,
    LiveStream,
    User,
    Media,
}

/// <summary>Who can see a report or a live stream.</summary>
public enum ContentVisibility
{
    Public,
    FriendsOnly,
}

/// <summary>Where a person appears on the shared map for a given audience.</summary>
public enum MapPresence
{
    Hidden,
    LastReportOnly,
    Realtime,
}

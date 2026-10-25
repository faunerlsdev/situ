using Situ.Domain.Anonymity;
using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>
/// Comment on a report or live stream. Can be anonymous: the same user gets the same
/// "Situero anónimo N" index inside one report, but never across reports (RF-ANO-03).
/// </summary>
public sealed class Comment : AggregateRoot
{
    private Comment()
    {
        Text = null!;
    }

    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? ParentCommentId { get; private set; }
    public string Text { get; private set; }
    public bool IsAnonymous { get; private set; }

    /// <summary>Index inside the report when anonymous (1, 2, 3...). Null when the author is public.</summary>
    public int? AnonymousIndex { get; private set; }

    public CommentStatus Status { get; private set; }

    public string? AnonymousDisplayName => IsAnonymous ? AnonymousIdentity.DisplayName(AnonymousIndex) : null;

    public static Comment Create(
        ContentTargetType targetType,
        Guid targetId,
        Guid authorId,
        string text,
        bool isAnonymous,
        int? anonymousIndex,
        Guid? parentCommentId,
        Guid reportAuthorId,
        DateTimeOffset now)
    {
        Guard.That(!isAnonymous || anonymousIndex is > 0, "comment.anonymous_index_required", "Anonymous comments need an index.");

        var comment = new Comment
        {
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            AuthorId = Guard.NotEmpty(authorId, nameof(authorId)),
            ParentCommentId = parentCommentId,
            Text = Guard.NotEmpty(text, nameof(text), 500),
            IsAnonymous = isAnonymous,
            AnonymousIndex = isAnonymous ? anonymousIndex : null,
            Status = CommentStatus.Visible,
        };

        comment.MarkCreated(now);
        comment.Raise(new CommentAddedEvent(comment.Id, targetId, reportAuthorId, isAnonymous));
        return comment;
    }

    public void Edit(string text, DateTimeOffset now)
    {
        Guard.That(Status == CommentStatus.Visible, "comment.not_visible", "The comment can no longer be edited.");
        Text = Guard.NotEmpty(text, nameof(text), 500);
        MarkUpdated(now);
    }

    public void Hide(DateTimeOffset now)
    {
        if (Status == CommentStatus.Removed)
        {
            return;
        }

        Status = CommentStatus.Hidden;
        MarkUpdated(now);
    }

    public void Restore(DateTimeOffset now)
    {
        Guard.That(Status == CommentStatus.Hidden, "comment.not_hidden", "Only a hidden comment can be restored.");
        Status = CommentStatus.Visible;
        MarkUpdated(now);
    }

    public void Remove(DateTimeOffset now)
    {
        Status = CommentStatus.Removed;
        MarkUpdated(now);
    }
}

/// <summary>
/// The report author is notified later even when the comment is anonymous.
/// This event deliberately omits the commenter id so a log line cannot reveal them (RF-ANO-07).
/// </summary>
public sealed record CommentAddedEvent(Guid CommentId, Guid ReportId, Guid ReportAuthorId, bool IsAnonymous) : DomainEvent;

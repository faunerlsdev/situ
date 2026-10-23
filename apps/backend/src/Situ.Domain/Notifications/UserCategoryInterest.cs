using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

/// <summary>Report category the user wants (or has muted) notifications for.</summary>
public sealed class UserCategoryInterest : AuditableEntity
{
    private UserCategoryInterest()
    {
    }

    public Guid UserId { get; private set; }
    public Guid CategoryId { get; private set; }
    public bool IsMuted { get; private set; }

    public static UserCategoryInterest Create(Guid userId, Guid categoryId, bool isMuted = false) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId)),
        IsMuted = isMuted,
    };

    public void SetMuted(bool isMuted) => IsMuted = isMuted;
}

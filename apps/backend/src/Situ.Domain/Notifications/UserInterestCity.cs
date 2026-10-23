using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

/// <summary>Extra city the user follows (receives important news from it), besides their home city.</summary>
public sealed class UserInterestCity : AuditableEntity
{
    private UserInterestCity()
    {
    }

    public Guid UserId { get; private set; }
    public Guid CityId { get; private set; }

    public static UserInterestCity Create(Guid userId, Guid cityId) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        CityId = Guard.NotEmpty(cityId, nameof(cityId)),
    };
}

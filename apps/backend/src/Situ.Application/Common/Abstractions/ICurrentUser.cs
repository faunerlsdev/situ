namespace Situ.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? DeviceId { get; }

    bool IsAuthenticated { get; }
}

namespace Situ.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}

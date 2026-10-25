using Situ.Application.Common.Abstractions;

namespace Situ.Api.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public Guid? DeviceId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirst("did")?.Value;
            return Guid.TryParse(value, out var deviceId) ? deviceId : null;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}

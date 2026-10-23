using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.RegisterUser;

public sealed record RegisterUserResponse(Guid UserId, UserStatus Status);

using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ConfirmEmail;

public sealed record ConfirmEmailResponse(Guid UserId, UserStatus Status);

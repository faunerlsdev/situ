using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ChangePassword;

public sealed class ChangePasswordHandler(
    IApplicationDbContext db,
    IClock clock,
    IPasswordHasher passwords,
    ICurrentUser currentUser)
    : IHandler<ChangePasswordRequest, Result>
{
    public async Task<Result> Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        if (!PasswordPolicy.IsValid(request.NewPassword))
        {
            return Result.Failure(IdentityErrors.InvalidPassword());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        if (!passwords.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(IdentityErrors.CurrentPasswordInvalid());
        }

        user.ChangePassword(passwords.Hash(request.NewPassword), clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

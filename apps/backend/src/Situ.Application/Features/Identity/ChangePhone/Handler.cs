using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ChangePhone;

public sealed record ChangePhoneRequest(Guid UserId, string? IpAddress, string CurrentPassword, string Phone);

public sealed class ChangePhoneHandler(IApplicationDbContext db, IClock clock, IPasswordHasher passwords)
    : IHandler<ChangePhoneRequest, Result>
{
    public async Task<Result> Handle(ChangePhoneRequest request, CancellationToken cancellationToken)
    {
        if (!PhoneNumber.TryCreate(request.Phone, out var phone))
        {
            return Result.Failure(IdentityErrors.InvalidPhone());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        if (!passwords.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(IdentityErrors.CurrentPasswordInvalid());
        }

        var now = clock.UtcNow;
        user.ChangePhone(phone);
        db.AuditLogs.Add(AuditLog.Record(
            user.Id,
            "users.change_phone",
            nameof(User),
            user.Id.ToString(),
            """{"phoneChanged":true}""",
            request.IpAddress,
            now));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ConfirmEmail;

public sealed class ConfirmEmailHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ConfirmEmailRequest, Result<ConfirmEmailResponse>>
{
    public async Task<Result<ConfirmEmailResponse>> Handle(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var hash = SecurityTokenCodec.Hash(request.Token.Trim());
        var token = await db.SecurityTokens.FirstOrDefaultAsync(
            item => item.TokenHash == hash && item.Purpose == SecurityTokenPurpose.EmailConfirmation,
            cancellationToken);
        if (token is null)
        {
            return Result<ConfirmEmailResponse>.Failure(IdentityErrors.TokenNotFound());
        }

        var now = clock.UtcNow;
        try
        {
            token.Consume(now);
        }
        catch (DomainException exception)
        {
            return Result<ConfirmEmailResponse>.Failure(IdentityErrors.FromDomain(exception));
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == token.UserId, cancellationToken);
        if (user is null)
        {
            return Result<ConfirmEmailResponse>.Failure(IdentityErrors.TokenNotFound());
        }

        user.ConfirmEmail(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<ConfirmEmailResponse>.Success(new ConfirmEmailResponse(user.Id, user.Status));
    }
}

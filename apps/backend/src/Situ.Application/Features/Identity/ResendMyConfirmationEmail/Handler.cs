using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.ResendConfirmationEmail;

namespace Situ.Application.Features.Identity.ResendMyConfirmationEmail;

public sealed record ResendMyConfirmationEmailRequest(Guid UserId);

/// <summary>Resends the confirmation mail for the signed-in user. The address never leaves the server.</summary>
public sealed class ResendMyConfirmationEmailHandler(
    IApplicationDbContext db,
    IHandler<ResendConfirmationEmailRequest, Result> resend)
    : IHandler<ResendMyConfirmationEmailRequest, Result>
{
    public async Task<Result> Handle(ResendMyConfirmationEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null || user.EmailConfirmedAt is not null)
        {
            return Result.Success();
        }

        return await resend.Handle(new ResendConfirmationEmailRequest(user.Email.Value), cancellationToken);
    }
}

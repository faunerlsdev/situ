using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;

namespace Situ.Application.Features.Profiles.CheckUserName;

public sealed record CheckUserNameRequest(Guid UserId, string? UserName);

public sealed record CheckUserNameResponse(bool Available);

public sealed class CheckUserNameValidator : AbstractValidator<CheckUserNameRequest>
{
    public CheckUserNameValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.UserName).NotEmpty().MaximumLength(30);
    }
}

/// <summary>
/// Taken and reserved names both answer <c>available: false</c>. The response has no other fields,
/// so a caller cannot tell a reserved word from an existing account.
/// </summary>
public sealed class CheckUserNameHandler(IApplicationDbContext db)
    : IHandler<CheckUserNameRequest, Result<CheckUserNameResponse>>
{
    public async Task<Result<CheckUserNameResponse>> Handle(CheckUserNameRequest request, CancellationToken cancellationToken)
    {
        if (!UserName.TryCreate(request.UserName, out var userName))
        {
            return Result<CheckUserNameResponse>.Success(new CheckUserNameResponse(false));
        }

        var settings = await ProfileRules.SettingsAsync(db, cancellationToken);
        var reserved = ProfileRules.List(settings, SystemSettingKeys.ProfileReservedUserNames);
        var blocked = ProfileRules.List(settings, SystemSettingKeys.ProfileBlockedTerms);
        if (reserved.Contains(userName.Value) || ProfileRules.ContainsBlocked(userName.Value, blocked))
        {
            var mine = await db.Users.AnyAsync(
                item => item.Id == request.UserId && item.UserName == userName,
                cancellationToken);
            return Result<CheckUserNameResponse>.Success(new CheckUserNameResponse(mine));
        }

        var taken = await db.Users.AnyAsync(
            item => item.Id != request.UserId && item.UserName == userName,
            cancellationToken);
        return Result<CheckUserNameResponse>.Success(new CheckUserNameResponse(!taken));
    }
}

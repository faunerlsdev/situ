using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Application.Features.Interaction;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Social;

namespace Situ.Application.Features.Social.InviteFriend;

public sealed record InviteFriendByEmailRequest(Guid UserId, string Email);

public sealed record InviteFriendByEmailResponse(string Status);

public sealed class InviteFriendByEmailValidator : AbstractValidator<InviteFriendByEmailRequest>
{
    public InviteFriendByEmailValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(254);
    }
}

public sealed class InviteFriendByEmailHandler(IApplicationDbContext db, IClock clock, IEmailSender email, IPublicLinks links)
    : IHandler<InviteFriendByEmailRequest, Result<InviteFriendByEmailResponse>>
{
    public async Task<Result<InviteFriendByEmailResponse>> Handle(InviteFriendByEmailRequest request, CancellationToken cancellationToken)
    {
        if (!Email.TryCreate(request.Email, out var address))
        {
            return Result<InviteFriendByEmailResponse>.Failure(Error.Validation("invitation.email_invalid", "Ese correo no es válido."));
        }

        var now = clock.UtcNow;
        var me = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (me is null)
        {
            return Result<InviteFriendByEmailResponse>.Success(new InviteFriendByEmailResponse(SocialRules.Sent));
        }

        var settings = await SocialRules.SettingsAsync(db, cancellationToken);
        var dailyCap = Math.Max(1, ReportSettings.Int(settings, SystemSettingKeys.FriendsMaxInvitationsPerDay, 10));
        var since = now.AddHours(-24);
        var today = await db.AuditLogs.CountAsync(
            item => item.ActorUserId == me.Id && item.Action == "friends.invitation" && item.OccurredAt >= since,
            cancellationToken);
        if (today >= dailyCap)
        {
            return Result<InviteFriendByEmailResponse>.Failure(SocialErrors.RateLimited());
        }

        Audit.Write(db, me.Id, "friends.invitation", "User", me.Id, null, now);

        var existing = await db.Users.FirstOrDefaultAsync(item => item.Email == address, cancellationToken);
        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        if (existing is not null && (!SocialRules.IsVisibleAccount(existing) || existing.IsMinor(now) || !SocialRules.PairAllowed(me, existing, now, allow) || await SocialRules.FullBlockBetweenAsync(db, me.Id, existing.Id, cancellationToken)))
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result<InviteFriendByEmailResponse>.Success(new InviteFriendByEmailResponse(SocialRules.Sent));
        }

        if (existing is not null && existing.Id != me.Id)
        {
            var already = await db.Friendships.AnyAsync(
                item => item.Status == FriendshipStatus.Pending && item.RequesterId == me.Id && item.AddresseeId == existing.Id,
                cancellationToken);
            var friends = await db.Friendships.AnyAsync(
                item => item.Status == FriendshipStatus.Accepted
                    && ((item.RequesterId == me.Id && item.AddresseeId == existing.Id)
                        || (item.RequesterId == existing.Id && item.AddresseeId == me.Id)),
                cancellationToken);
            if (!friends && !already)
            {
                db.Friendships.Add(Friendship.Request(me.Id, existing.Id, now));
            }
        }

        if (existing is null)
        {
            var open = await db.FriendInvitations.AnyAsync(
                item => item.InviterId == me.Id && item.Email == address.Value && item.Status == FriendInvitationStatus.Pending && item.ExpiresAt > now,
                cancellationToken);
            if (!open)
            {
                var plain = SecurityTokenCodec.CreatePlain();
                db.FriendInvitations.Add(FriendInvitation.Create(me.Id, address.Value, SecurityTokenCodec.Hash(plain), now));
                await db.SaveChangesAsync(cancellationToken);
                await email.SendAsync(
                    address.Value,
                    "SITU: te invitaron a ser amigo",
                    $"Alguien te invitó a SITU. El enlace se usa una sola vez y vence en 30 días. Si ya tienes cuenta, ábrelo con este mismo correo. Si no, crea la cuenta con este correo y vuelve a abrirlo.\n{links.FriendInvitation(plain)}",
                    cancellationToken);
                return Result<InviteFriendByEmailResponse>.Success(new InviteFriendByEmailResponse(SocialRules.Sent));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        if (existing is { Status: UserStatus.Active } && existing.Id != me.Id)
        {
            await email.SendAsync(
                address.Value,
                "SITU: te invitaron a ser amigo",
                "Alguien te invitó a ser su amigo en SITU. Entra a la app y revisa tus solicitudes. El mensaje es el mismo si la invitación ya estaba en camino.",
                cancellationToken);
        }

        return Result<InviteFriendByEmailResponse>.Success(new InviteFriendByEmailResponse(SocialRules.Sent));
    }
}

public sealed record AcceptFriendInvitationRequest(Guid UserId, string Token);

public sealed record AcceptFriendInvitationResponse(string Status);

public sealed class AcceptFriendInvitationValidator : AbstractValidator<AcceptFriendInvitationRequest>
{
    public AcceptFriendInvitationValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MaximumLength(200);
    }
}

public sealed class AcceptFriendInvitationHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AcceptFriendInvitationRequest, Result<AcceptFriendInvitationResponse>>
{
    public async Task<Result<AcceptFriendInvitationResponse>> Handle(AcceptFriendInvitationRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var hash = SecurityTokenCodec.Hash(request.Token.Trim());
        var invitation = await db.FriendInvitations.FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        var me = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (invitation is null || me is null || !invitation.IsOpen(now) || !string.Equals(invitation.Email, me.Email.Value, StringComparison.Ordinal))
        {
            return Result<AcceptFriendInvitationResponse>.Failure(SocialErrors.InvitationInvalid());
        }

        var inviter = await db.Users.FirstOrDefaultAsync(item => item.Id == invitation.InviterId, cancellationToken);
        var allow = await SocialRules.AllowAdultMinorAsync(db, cancellationToken);
        if (inviter is null
            || inviter.Status != UserStatus.Active
            || !SocialRules.PairAllowed(me, inviter, now, allow)
            || await SocialRules.FullBlockBetweenAsync(db, me.Id, inviter.Id, cancellationToken))
        {
            return Result<AcceptFriendInvitationResponse>.Failure(SocialErrors.InvitationInvalid());
        }

        invitation.Accept(me.Id, now);
        var friends = await db.Friendships.AnyAsync(
            item => item.Status == FriendshipStatus.Accepted
                && ((item.RequesterId == inviter.Id && item.AddresseeId == me.Id)
                    || (item.RequesterId == me.Id && item.AddresseeId == inviter.Id)),
            cancellationToken);
        if (friends)
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result<AcceptFriendInvitationResponse>.Success(new AcceptFriendInvitationResponse("friends"));
        }

        var pending = await db.Friendships.AnyAsync(
            item => item.Status == FriendshipStatus.Pending && item.RequesterId == inviter.Id && item.AddresseeId == me.Id,
            cancellationToken);
        if (!pending)
        {
            db.Friendships.Add(Friendship.Request(inviter.Id, me.Id, now));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<AcceptFriendInvitationResponse>.Success(new AcceptFriendInvitationResponse("pending"));
    }
}

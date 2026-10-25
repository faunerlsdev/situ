using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Social;

namespace Situ.Application.Features.Interaction.Blocks;

public sealed record BlockUserRequest(Guid UserId, Guid BlockedUserId);

public sealed record BlockUserResponse(bool Blocked);

public sealed class BlockUserHandler(IApplicationDbContext db, IClock clock)
    : IHandler<BlockUserRequest, Result<BlockUserResponse>>
{
    public async Task<Result<BlockUserResponse>> Handle(BlockUserRequest request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.BlockedUserId)
        {
            return Result<BlockUserResponse>.Failure(InteractionErrors.BlockSelf());
        }

        var exists = await db.Users.AnyAsync(item => item.Id == request.BlockedUserId, cancellationToken);
        if (!exists)
        {
            return Result<BlockUserResponse>.Failure(InteractionErrors.BlockNotFound());
        }

        await EnsureFullAsync(db, request.UserId, request.BlockedUserId, clock.UtcNow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result<BlockUserResponse>.Success(new BlockUserResponse(true));
    }

    internal static async Task EnsureFullAsync(
        IApplicationDbContext db,
        Guid blockerId,
        Guid blockedUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var already = await db.UserBlocks.AnyAsync(
            item => item.BlockerId == blockerId && item.BlockedUserId == blockedUserId && item.Scope == UserBlockScope.Full,
            cancellationToken);
        if (!already)
        {
            db.UserBlocks.Add(UserBlock.CreateFull(blockerId, blockedUserId, now));
        }

        await InteractionAccess.SeverRelationshipAsync(db, blockerId, blockedUserId, now, cancellationToken);
    }
}

public sealed record UnblockUserRequest(Guid UserId, Guid BlockId);

public sealed class UnblockUserHandler(IApplicationDbContext db)
    : IHandler<UnblockUserRequest, Result>
{
    public async Task<Result> Handle(UnblockUserRequest request, CancellationToken cancellationToken)
    {
        var block = await db.UserBlocks.FirstOrDefaultAsync(
            item => item.Id == request.BlockId && item.BlockerId == request.UserId,
            cancellationToken);
        block ??= await db.UserBlocks.FirstOrDefaultAsync(
            item => item.BlockerId == request.UserId
                && item.BlockedUserId == request.BlockId
                && item.Scope == UserBlockScope.Full,
            cancellationToken);
        if (block is null)
        {
            return Result.Failure(InteractionErrors.BlockNotFound());
        }

        db.UserBlocks.Remove(block);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListBlocksRequest(Guid UserId);

public sealed record BlockListItem(Guid BlockId, string Scope, Guid? UserId, string DisplayName, string? OriginLabel);

public sealed record ListBlocksResponse(IReadOnlyList<BlockListItem> Blocks);

public sealed class ListBlocksHandler(IApplicationDbContext db)
    : IHandler<ListBlocksRequest, Result<ListBlocksResponse>>
{
    public async Task<Result<ListBlocksResponse>> Handle(ListBlocksRequest request, CancellationToken cancellationToken)
    {
        var rows = await db.UserBlocks
            .Where(item => item.BlockerId == request.UserId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var fullIds = rows.Where(item => item.Scope == UserBlockScope.Full).Select(item => item.BlockedUserId).Distinct().ToArray();
        var users = await db.Users.Where(item => fullIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => fullIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var views = rows.Select(row =>
        {
            if (row.Scope == UserBlockScope.AnonymousContentOnly)
            {
                return new BlockListItem(row.Id, row.Scope.ToString(), null, "Usuario anónimo", OriginLabel(row));
            }

            var user = users.FirstOrDefault(item => item.Id == row.BlockedUserId);
            var profile = profiles.FirstOrDefault(item => item.UserId == row.BlockedUserId);
            return new BlockListItem(
                row.Id,
                row.Scope.ToString(),
                row.BlockedUserId,
                profile?.DisplayName ?? user?.UserName.Value ?? "Usuario",
                null);
        }).ToList();
        return Result<ListBlocksResponse>.Success(new ListBlocksResponse(views));
    }

    internal static string OriginLabel(UserBlock block)
    {
        var kind = block.OriginTargetType == ContentTargetType.Comment ? "comentario" : "reporte";
        var when = block.OriginAt ?? block.CreatedAt;
        return $"desde un {kind} del {when.Day} {Month(when.Month)}";
    }

    private static string Month(int month) => month switch
    {
        1 => "ene",
        2 => "feb",
        3 => "mar",
        4 => "abr",
        5 => "may",
        6 => "jun",
        7 => "jul",
        8 => "ago",
        9 => "sep",
        10 => "oct",
        11 => "nov",
        _ => "dic",
    };
}

public sealed record BlockContentAuthorRequest(Guid UserId, ContentTargetType TargetType, Guid TargetId);

public sealed class BlockContentAuthorValidator : AbstractValidator<BlockContentAuthorRequest>
{
    public BlockContentAuthorValidator()
    {
        RuleFor(request => request.TargetType).Must(type => type is ContentTargetType.Report or ContentTargetType.Comment);
    }
}

public sealed class BlockContentAuthorHandler(IApplicationDbContext db, IClock clock)
    : IHandler<BlockContentAuthorRequest, Result<BlockUserResponse>>
{
    public async Task<Result<BlockUserResponse>> Handle(BlockContentAuthorRequest request, CancellationToken cancellationToken)
    {
        Guid? authorId;
        DateTimeOffset originAt;
        if (request.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.Where(item => item.Id == request.TargetId).Select(item => new { item.AuthorId, item.CreatedAt }).FirstOrDefaultAsync(cancellationToken);
            authorId = report?.AuthorId;
            originAt = report?.CreatedAt ?? clock.UtcNow;
        }
        else
        {
            var comment = await db.Comments.Where(item => item.Id == request.TargetId).Select(item => new { item.AuthorId, item.CreatedAt }).FirstOrDefaultAsync(cancellationToken);
            authorId = comment?.AuthorId;
            originAt = comment?.CreatedAt ?? clock.UtcNow;
        }

        if (authorId is null)
        {
            return Result<BlockUserResponse>.Failure(InteractionErrors.NotFound());
        }

        if (authorId == request.UserId)
        {
            return Result<BlockUserResponse>.Failure(InteractionErrors.BlockSelf());
        }

        var now = clock.UtcNow;
        var existing = await db.UserBlocks.AnyAsync(
            item => item.BlockerId == request.UserId && item.BlockedUserId == authorId && item.Scope == UserBlockScope.AnonymousContentOnly,
            cancellationToken);
        if (!existing)
        {
            db.UserBlocks.Add(UserBlock.HideAnonymousContent(request.UserId, authorId.Value, request.TargetType, request.TargetId, originAt, now));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<BlockUserResponse>.Success(new BlockUserResponse(true));
    }
}

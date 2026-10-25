using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction.AccountDeletion;
using Situ.Application.Features.Interaction.Blocks;
using Situ.Application.Features.Interaction.Comments;
using Situ.Application.Features.Interaction.Complaints;
using Situ.Application.Features.Interaction.Reactions;
using Situ.Domain.Common;
using Situ.Domain.Moderation;
using Situ.Domain.Reports;

namespace Situ.Api.Endpoints.Interaction;

public sealed class CommentEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/reports/{id:guid}/comments", Add)
            .WithName("AddComment")
            .WithTags("Comments")
            .Produces<AddCommentResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
        group.MapGet("/reports/{id:guid}/comments", List)
            .WithName("ListComments")
            .WithTags("Comments")
            .Produces<ListCommentsResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);
        group.MapPatch("/comments/{id:guid}", Edit)
            .WithName("EditComment")
            .WithTags("Comments")
            .Produces<EditCommentResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
        group.MapDelete("/comments/{id:guid}", Delete)
            .WithName("DeleteComment")
            .WithTags("Comments")
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
    }

    private static async Task<IResult> Add(Guid id, CommentBody body, ICurrentUser currentUser, IHandler<AddCommentRequest, Result<AddCommentResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new AddCommentRequest(userId, id, body.Text, body.IsAnonymous, body.ParentCommentId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> List(Guid id, string? cursor, int? limit, ICurrentUser currentUser, IHandler<ListCommentsRequest, Result<ListCommentsResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListCommentsRequest(currentUser.UserId, id, cursor, limit is > 0 and <= 50 ? limit.Value : 20), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Edit(Guid id, TextBody body, ICurrentUser currentUser, IHandler<EditCommentRequest, Result<EditCommentResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new EditCommentRequest(userId, id, body.Text), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Delete(Guid id, ICurrentUser currentUser, IHandler<DeleteCommentRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new DeleteCommentRequest(userId, id), cancellationToken)).ToHttpResult();
    }

    private sealed record CommentBody(string Text, bool IsAnonymous, Guid? ParentCommentId);
    private sealed record TextBody(string Text);
}

public sealed class ReactionEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/reports/{id:guid}/reaction", (Guid id, ReactionBody body, ICurrentUser user, IHandler<SetReactionRequest, Result<SetReactionResponse>> handler, CancellationToken cancellationToken) =>
                Set(ContentTargetType.Report, id, body, user, handler, cancellationToken))
            .WithName("SetReportReaction")
            .WithTags("Reactions")
            .Produces<SetReactionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
        group.MapDelete("/reports/{id:guid}/reaction", (Guid id, ICurrentUser user, IHandler<RemoveReactionRequest, Result<SetReactionResponse>> handler, CancellationToken cancellationToken) =>
                Remove(ContentTargetType.Report, id, user, handler, cancellationToken))
            .WithName("RemoveReportReaction")
            .WithTags("Reactions")
            .Produces<SetReactionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
        group.MapPut("/comments/{id:guid}/reaction", (Guid id, ReactionBody body, ICurrentUser user, IHandler<SetReactionRequest, Result<SetReactionResponse>> handler, CancellationToken cancellationToken) =>
                Set(ContentTargetType.Comment, id, body, user, handler, cancellationToken))
            .WithName("SetCommentReaction")
            .WithTags("Reactions")
            .Produces<SetReactionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
        group.MapDelete("/comments/{id:guid}/reaction", (Guid id, ICurrentUser user, IHandler<RemoveReactionRequest, Result<SetReactionResponse>> handler, CancellationToken cancellationToken) =>
                Remove(ContentTargetType.Comment, id, user, handler, cancellationToken))
            .WithName("RemoveCommentReaction")
            .WithTags("Reactions")
            .Produces<SetReactionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
    }

    private static async Task<IResult> Set(ContentTargetType targetType, Guid id, ReactionBody body, ICurrentUser currentUser, IHandler<SetReactionRequest, Result<SetReactionResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || !Enum.TryParse<ReactionType>(body.Type, out var type))
        {
            return currentUser.UserId is null ? Results.Unauthorized() : Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Elige una reacción válida."] });
        }

        return (await handler.Handle(new SetReactionRequest(userId, targetType, id, type), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Remove(ContentTargetType targetType, Guid id, ICurrentUser currentUser, IHandler<RemoveReactionRequest, Result<SetReactionResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new RemoveReactionRequest(userId, targetType, id), cancellationToken)).ToHttpResult();
    }

    private sealed record ReactionBody(string Type);
}

public sealed class ComplaintEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/complaints", File)
            .WithName("FileComplaint")
            .WithTags("Complaints")
            .Produces<FileComplaintResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
        group.MapGet("/moderation/complaints", List)
            .WithName("ListComplaints")
            .WithTags("Moderation")
            .Produces<ListComplaintsResponse>()
            .RequireAuthorization(AuthorizationPolicies.Staff);
        group.MapPost("/moderation/complaints/{id:guid}/start-review", Start)
            .WithName("StartComplaintReview")
            .WithTags("Moderation")
            .RequireAuthorization(AuthorizationPolicies.Staff);
        group.MapPost("/moderation/complaints/{id:guid}/uphold", Uphold)
            .WithName("UpholdComplaint")
            .WithTags("Moderation")
            .RequireAuthorization(AuthorizationPolicies.Staff);
        group.MapPost("/moderation/complaints/{id:guid}/dismiss", Dismiss)
            .WithName("DismissComplaint")
            .WithTags("Moderation")
            .RequireAuthorization(AuthorizationPolicies.Staff);
        group.MapPost("/moderation/anonymous-identity/reveal", Reveal)
            .WithName("RevealAnonymousIdentity")
            .WithTags("Moderation")
            .Produces<RevealAnonymousIdentityResponse>()
            .RequireAuthorization(AuthorizationPolicies.Staff);
    }

    private static async Task<IResult> File(ComplaintBody body, ICurrentUser currentUser, IHandler<FileComplaintRequest, Result<FileComplaintResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId
            || !Enum.TryParse<ContentTargetType>(body.TargetType, out var targetType)
            || !Enum.TryParse<ComplaintReason>(body.Reason, out var reason))
        {
            return currentUser.UserId is null
                ? Results.Unauthorized()
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["target"] = ["Revisa el tipo y el motivo."] });
        }

        return (await handler.Handle(new FileComplaintRequest(userId, targetType, body.TargetId, reason, body.Details), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> List(string? status, string? reason, ICurrentUser currentUser, IHandler<ListComplaintsRequest, Result<ListComplaintsResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new ListComplaintsRequest(userId, status, reason), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Start(Guid id, ICurrentUser currentUser, IHandler<StartComplaintReviewRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new StartComplaintReviewRequest(userId, id), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Uphold(Guid id, ResolutionBody body, ICurrentUser currentUser, IHandler<UpholdComplaintRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new UpholdComplaintRequest(userId, id, body.Resolution, body.Action), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Dismiss(Guid id, DismissBody body, ICurrentUser currentUser, IHandler<DismissComplaintRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new DismissComplaintRequest(userId, id, body.Resolution), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Reveal(RevealBody body, ICurrentUser currentUser, IHandler<RevealAnonymousIdentityRequest, Result<RevealAnonymousIdentityResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || !Enum.TryParse<ContentTargetType>(body.TargetType, out var targetType))
        {
            return currentUser.UserId is null ? Results.Unauthorized() : Results.ValidationProblem(new Dictionary<string, string[]> { ["targetType"] = ["El tipo no es válido."] });
        }

        return (await handler.Handle(new RevealAnonymousIdentityRequest(userId, targetType, body.TargetId, body.ComplaintId, body.Justification), cancellationToken)).ToHttpResult();
    }

    private sealed record ComplaintBody(string TargetType, Guid TargetId, string Reason, string? Details);
    private sealed record ResolutionBody(string Resolution, string Action);
    private sealed record DismissBody(string Resolution);
    private sealed record RevealBody(string TargetType, Guid TargetId, Guid? ComplaintId, string Justification);
}

public sealed class BlockEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/blocks", Block)
            .WithName("BlockUser")
            .WithTags("Blocks")
            .Produces<BlockUserResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);
        group.MapDelete("/blocks/{blockId:guid}", Unblock)
            .WithName("UnblockUser")
            .WithTags("Blocks")
            .RequireAuthorization(AuthorizationPolicies.User);
        group.MapGet("/blocks", List)
            .WithName("ListBlocks")
            .WithTags("Blocks")
            .Produces<ListBlocksResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);
        group.MapPost("/reports/{id:guid}/block-author", (Guid id, ICurrentUser user, IHandler<BlockContentAuthorRequest, Result<BlockUserResponse>> handler, CancellationToken cancellationToken) =>
                BlockAuthor(ContentTargetType.Report, id, user, handler, cancellationToken))
            .WithName("BlockReportAuthor")
            .WithTags("Blocks")
            .Produces<BlockUserResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);
        group.MapPost("/comments/{id:guid}/block-author", (Guid id, ICurrentUser user, IHandler<BlockContentAuthorRequest, Result<BlockUserResponse>> handler, CancellationToken cancellationToken) =>
                BlockAuthor(ContentTargetType.Comment, id, user, handler, cancellationToken))
            .WithName("BlockCommentAuthor")
            .WithTags("Blocks")
            .Produces<BlockUserResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> Block(BlockBody body, ICurrentUser currentUser, IHandler<BlockUserRequest, Result<BlockUserResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new BlockUserRequest(userId, body.UserId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Unblock(Guid blockId, ICurrentUser currentUser, IHandler<UnblockUserRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new UnblockUserRequest(actorId, blockId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> List(ICurrentUser currentUser, IHandler<ListBlocksRequest, Result<ListBlocksResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new ListBlocksRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> BlockAuthor(ContentTargetType targetType, Guid id, ICurrentUser currentUser, IHandler<BlockContentAuthorRequest, Result<BlockUserResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new BlockContentAuthorRequest(userId, targetType, id), cancellationToken)).ToHttpResult();
    }

    private sealed record BlockBody(Guid UserId);
}

public sealed class AccountDeletionEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/me/deletion-request", RequestDeletion)
            .WithName("RequestAccountDeletion")
            .WithTags("Identity")
            .Produces<RequestAccountDeletionResponse>()
            .RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("interaction-write");
        group.MapPost("/me/deletion-request/cancel", Cancel)
            .WithName("CancelAccountDeletion")
            .WithTags("Identity")
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> RequestDeletion(PasswordBody body, ICurrentUser currentUser, IHandler<RequestAccountDeletionRequest, Result<RequestAccountDeletionResponse>> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new RequestAccountDeletionRequest(userId, body.Password), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Cancel(ICurrentUser currentUser, IHandler<CancelAccountDeletionRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new CancelAccountDeletionRequest(userId), cancellationToken)).ToHttpResult();
    }

    private sealed record PasswordBody(string Password);
}

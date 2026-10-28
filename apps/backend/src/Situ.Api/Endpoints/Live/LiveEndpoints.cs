using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction.Complaints;
using Situ.Application.Features.Live;
using Situ.Domain.Moderation;

namespace Situ.Api.Endpoints.Live;

public sealed class LiveEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        var live = group.MapGroup("/live").RequireAuthorization(AuthorizationPolicies.User).WithTags("Live");
        live.MapPost("/", Create).WithName("CreateLive").RequireRateLimiting("live").Produces<CreateLiveResponse>();
        live.MapPost("/{id}/end", End).WithName("EndLive").RequireRateLimiting("live");
        live.MapGet("/", List).WithName("ListLive").RequireRateLimiting("live").Produces<ListLiveResponse>();
        live.MapGet("/{id}", Get).WithName("GetLive").RequireRateLimiting("live").Produces<LiveCard>();
        live.MapPost("/{id}/watch", Watch).WithName("WatchLive").RequireRateLimiting("live").Produces<WatchLiveResponse>();
        live.MapPost("/{id}/report", Report).WithName("ReportLive").RequireRateLimiting("live").Produces<FileComplaintResponse>();

        var admin = group.MapGroup("/admin/live")
            .RequireAuthorization(AuthorizationPolicies.StaffMfa)
            .RequireRateLimiting("admin")
            .WithTags("Admin");
        admin.MapGet("/", AdminList).WithName("ListAdminLive").Produces<ListAdminLiveResponse>();
        admin.MapPost("/{id}/end", AdminEnd).WithName("AdminEndLive");
        admin.MapPost("/{id}/hide", Hide).WithName("HideLive");
        admin.MapGet("/{id}/complaints", Complaints).WithName("ListLiveComplaints").Produces<ListComplaintsResponse>();
    }

    private static async Task<IResult> Create(CreateBody body, ICurrentUser current, IHandler<CreateLiveRequest, Result<CreateLiveResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new CreateLiveRequest(userId, body.Visibility ?? "", body.Title ?? "", body.CategoryId, body.Latitude, body.Longitude), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> End(string id, ICurrentUser current, IHandler<EndLiveRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new EndLiveRequest(userId, id, Staff: false), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> List(ICurrentUser current, IHandler<ListLiveRequest, Result<ListLiveResponse>> handler, CancellationToken cancellationToken) =>
        current.UserId is { } userId
            ? (await handler.Handle(new ListLiveRequest(userId), cancellationToken)).ToHttpResult()
            : Results.Unauthorized();

    private static async Task<IResult> Get(string id, ICurrentUser current, IHandler<GetLiveRequest, Result<LiveCard>> handler, CancellationToken cancellationToken) =>
        current.UserId is { } userId
            ? (await handler.Handle(new GetLiveRequest(userId, id), cancellationToken)).ToHttpResult()
            : Results.Unauthorized();

    private static async Task<IResult> Watch(string id, ICurrentUser current, IHandler<WatchLiveRequest, Result<WatchLiveResponse>> handler, CancellationToken cancellationToken) =>
        current.UserId is { } userId
            ? (await handler.Handle(new WatchLiveRequest(userId, id), cancellationToken)).ToHttpResult()
            : Results.Unauthorized();

    private static async Task<IResult> Report(string id, ReportBody body, ICurrentUser current, IHandler<ReportLiveRequest, Result<FileComplaintResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId || !Enum.TryParse<ComplaintReason>(body.Reason, out var reason))
        {
            return current.UserId is null
                ? Results.Unauthorized()
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Revisa el motivo."] });
        }

        return (await handler.Handle(new ReportLiveRequest(userId, id, reason, body.Details), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> AdminList(ICurrentUser current, IHandler<ListAdminLiveRequest, Result<ListAdminLiveResponse>> handler, CancellationToken cancellationToken) =>
        current.UserId is { } userId
            ? (await handler.Handle(new ListAdminLiveRequest(userId), cancellationToken)).ToHttpResult()
            : Results.Unauthorized();

    private static async Task<IResult> AdminEnd(string id, ICurrentUser current, IHandler<EndLiveRequest, Result> handler, CancellationToken cancellationToken) =>
        current.UserId is { } userId
            ? (await handler.Handle(new EndLiveRequest(userId, id, Staff: true), cancellationToken)).ToHttpResult()
            : Results.Unauthorized();

    private static async Task<IResult> Hide(string id, HideBody body, ICurrentUser current, IHandler<HideLiveRequest, Result> handler, CancellationToken cancellationToken) =>
        current.UserId is { } userId
            ? (await handler.Handle(new HideLiveRequest(userId, id, body.Reason ?? ""), cancellationToken)).ToHttpResult()
            : Results.Unauthorized();

    private static async Task<IResult> Complaints(string id, ICurrentUser current, IHandler<ListLiveComplaintsRequest, Result<ListComplaintsResponse>> handler, CancellationToken cancellationToken) =>
        current.UserId is { } userId
            ? (await handler.Handle(new ListLiveComplaintsRequest(userId, id), cancellationToken)).ToHttpResult()
            : Results.Unauthorized();

    private sealed record CreateBody(string? Visibility, string? Title, Guid? CategoryId, double? Latitude, double? Longitude);

    private sealed record ReportBody(string? Reason, string? Details);

    private sealed record HideBody(string? Reason);
}

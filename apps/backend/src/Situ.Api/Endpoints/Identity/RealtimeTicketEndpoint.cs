using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.RealtimeTickets;

namespace Situ.Api.Endpoints.Identity;

public sealed class RealtimeTicketEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/realtime-ticket", Handle)
            .WithName("IssueRealtimeTicket")
            .WithTags("Identity")
            .WithSummary("Ticket de un solo uso, 30 segundos, para conectar el mapa en tiempo real desde la web.")
            .Produces<IssueRealtimeTicketResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> Handle(
        HttpContext http,
        ICurrentUser current,
        IHandler<IssueRealtimeTicketRequest, Result<IssueRealtimeTicketResponse>> handler,
        CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var isStaff = http.User.IsInRole(AuthorizationPolicies.Staff)
            || string.Equals(http.User.FindFirst("role")?.Value, AuthorizationPolicies.Staff, StringComparison.Ordinal);
        var result = await handler.Handle(new IssueRealtimeTicketRequest(userId, current.DeviceId, isStaff), cancellationToken);
        return result.ToHttpResult();
    }
}

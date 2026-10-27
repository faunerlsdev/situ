using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.RealtimeTickets;

public sealed record IssueRealtimeTicketRequest(Guid UserId, Guid? DeviceId, bool IsStaff);

public sealed record IssueRealtimeTicketResponse(string Ticket, DateTimeOffset ExpiresAt);

public sealed class IssueRealtimeTicketHandler(IApplicationDbContext db, IClock clock, ITokenService tokens)
    : IHandler<IssueRealtimeTicketRequest, Result<IssueRealtimeTicketResponse>>
{
    public async Task<Result<IssueRealtimeTicketResponse>> Handle(IssueRealtimeTicketRequest request, CancellationToken cancellationToken)
    {
        if (request.IsStaff)
        {
            return Result<IssueRealtimeTicketResponse>.Failure(Error.Forbidden("realtime_ticket.staff_forbidden", "Esta cuenta no abre el mapa en tiempo real."));
        }

        if (request.DeviceId is not { } deviceId)
        {
            return Result<IssueRealtimeTicketResponse>.Failure(Error.Unauthorized("realtime_ticket.device_required", "Entra de nuevo para abrir el mapa en tiempo real."));
        }

        var device = await db.UserDevices.FirstOrDefaultAsync(item => item.Id == deviceId && item.UserId == request.UserId, cancellationToken);
        if (device is null || !device.IsActive || device.Platform != DevicePlatform.Web)
        {
            return Result<IssueRealtimeTicketResponse>.Failure(Error.Unauthorized("realtime_ticket.device_required", "Entra de nuevo desde este navegador para abrir el mapa en tiempo real."));
        }

        var now = clock.UtcNow;
        var plain = tokens.CreateRefreshToken();
        var ticket = RealtimeTicket.Issue(request.UserId, device.Id, tokens.Hash(plain), now);
        db.RealtimeTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        return Result<IssueRealtimeTicketResponse>.Success(new IssueRealtimeTicketResponse(plain, ticket.ExpiresAt));
    }
}

public sealed record ConsumeRealtimeTicketRequest(string Ticket, Guid? ExpectedUserId);

public sealed record ConsumeRealtimeTicketResponse(Guid UserId, Guid DeviceId);

public sealed class ConsumeRealtimeTicketHandler(IApplicationDbContext db, IClock clock, ITokenService tokens)
    : IHandler<ConsumeRealtimeTicketRequest, Result<ConsumeRealtimeTicketResponse>>
{
    public async Task<Result<ConsumeRealtimeTicketResponse>> Handle(ConsumeRealtimeTicketRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ticket))
        {
            return Reject("realtime_ticket.invalid");
        }

        var ticket = await db.RealtimeTickets.FirstOrDefaultAsync(item => item.TokenHash == tokens.Hash(request.Ticket.Trim()), cancellationToken);
        if (ticket is null)
        {
            return Reject("realtime_ticket.invalid");
        }

        var now = clock.UtcNow;
        var status = ticket.Status(now, request.ExpectedUserId);
        if (status != RealtimeTicketStatus.Valid)
        {
            return Reject(status switch
            {
                RealtimeTicketStatus.Used => "realtime_ticket.used",
                RealtimeTicketStatus.Expired => "realtime_ticket.expired",
                RealtimeTicketStatus.OtherUser => "realtime_ticket.user_mismatch",
                _ => "realtime_ticket.invalid",
            });
        }

        ticket.Consume(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<ConsumeRealtimeTicketResponse>.Success(new ConsumeRealtimeTicketResponse(ticket.UserId, ticket.DeviceId));
    }

    private static Result<ConsumeRealtimeTicketResponse> Reject(string code) =>
        Result<ConsumeRealtimeTicketResponse>.Failure(Error.Unauthorized(code, "No se pudo abrir el mapa en tiempo real."));
}

using Microsoft.Extensions.DependencyInjection;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.RealtimeTickets;

namespace Situ.Api.Realtime;

public static class RealtimeTicketExchange
{
    public static async Task<string?> TryExchangeAsync(IServiceProvider services, string ticket, CancellationToken cancellationToken)
    {
        var handler = services.GetRequiredService<IHandler<ConsumeRealtimeTicketRequest, Result<ConsumeRealtimeTicketResponse>>>();
        var consumed = await handler.Handle(new ConsumeRealtimeTicketRequest(ticket, null), cancellationToken);
        return consumed.IsFailure || consumed.Value is null ? null : consumed.Value.AccessToken;
    }
}

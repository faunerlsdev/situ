using Microsoft.Extensions.DependencyInjection;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.RealtimeTickets;
using Situ.Domain.Identity;

namespace Situ.Api.Realtime;

public static class RealtimeTicketExchange
{
    public static async Task<string?> TryExchangeAsync(IServiceProvider services, string ticket, CancellationToken cancellationToken)
    {
        var handler = services.GetRequiredService<IHandler<ConsumeRealtimeTicketRequest, Result<ConsumeRealtimeTicketResponse>>>();
        var consumed = await handler.Handle(new ConsumeRealtimeTicketRequest(ticket, null), cancellationToken);
        if (consumed.IsFailure || consumed.Value is null)
        {
            return null;
        }

        var clock = services.GetRequiredService<IClock>();
        var tokens = services.GetRequiredService<ITokenService>();
        var expires = clock.UtcNow.Add(SessionPolicy.AccessTokenLifetime);
        return tokens.CreateAccessToken(consumed.Value.UserId, expires, deviceId: consumed.Value.DeviceId);
    }
}

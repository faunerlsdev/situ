using Microsoft.AspNetCore.SignalR;
using Situ.Application.Common.Abstractions;

namespace Situ.Api.Realtime;

public sealed class SignalRMapZonePublisher(IHubContext<MapHub> hub) : IMapZonePublisher
{
    public Task PublishAsync(string cellId, IReadOnlyList<MapZoneSignal> events, CancellationToken cancellationToken) =>
        hub.Clients.Group(cellId).SendAsync("zone", events, cancellationToken);
}

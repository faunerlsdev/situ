using Microsoft.AspNetCore.SignalR;
using Situ.Application.Features.Live;

namespace Situ.Api.Realtime;

public sealed class SignalRLiveNotifier(IHubContext<MapHub> hub) : ILiveNotifier
{
    public Task ChangedAsync(IReadOnlyCollection<string> groups, string publicId, string change, CancellationToken cancellationToken) =>
        hub.Clients.Groups(groups).SendAsync("live", new LiveSignal(publicId, change), cancellationToken);

    private sealed record LiveSignal(string PublicId, string Change);
}

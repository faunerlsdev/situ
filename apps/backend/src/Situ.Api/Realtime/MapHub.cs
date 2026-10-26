using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Situ.Api.Auth;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.MapRealtime;
using Situ.Application.Features.MapRealtime.AuthorizeMapConnection;
using Situ.Application.Features.MapRealtime.GetMapRealtimeConfig;

namespace Situ.Api.Realtime;

[Authorize(Policy = AuthorizationPolicies.User)]
public sealed class MapHub(
    IHandler<AuthorizeMapConnectionRequest, Result<AuthorizeMapConnectionResponse>> authorize,
    IHandler<GetMapRealtimeConfigRequest, Result<MapRealtimeView>> config,
    MapSubscriptionBook book,
    IClock clock,
    ILogger<MapHub> logger) : Hub
{
    private const string LimitsKey = "map-limits";
    private const string ExpiryKey = "map-expiry";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (!Guid.TryParse(user?.FindFirst("sub")?.Value, out var userId))
        {
            Reject("map.device_required");
            return;
        }

        Guid? deviceId = Guid.TryParse(user?.FindFirst("did")?.Value, out var parsed) ? parsed : null;
        var isStaff = user?.IsInRole(AuthorizationPolicies.Staff) == true
            || string.Equals(user?.FindFirst("role")?.Value, AuthorizationPolicies.Staff, StringComparison.Ordinal);
        DateTimeOffset? expiresAt = null;
        if (long.TryParse(user?.FindFirst("exp")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        var gate = await authorize.Handle(new AuthorizeMapConnectionRequest(userId, deviceId, isStaff, expiresAt), Context.ConnectionAborted);
        if (gate.IsFailure)
        {
            Reject(gate.Error!.Code);
            return;
        }

        var limits = await config.Handle(new GetMapRealtimeConfigRequest(), Context.ConnectionAborted);
        if (limits.IsFailure)
        {
            Reject("map.config_unavailable");
            return;
        }

        book.Connect(Context.ConnectionId);
        Context.Items[LimitsKey] = limits.Value;
        logger.LogInformation(
            "Map hub connections {Connections} subscribed cells {Cells}.",
            book.ConnectionCount,
            book.SubscribedCellCount);
        var expiry = new CancellationTokenSource();
        Context.Items[ExpiryKey] = expiry;
        _ = CloseWhenExpiredAsync(Context, gate.Value!.AccessExpiresAt, expiry.Token);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[ExpiryKey] is CancellationTokenSource expiry)
        {
            await expiry.CancelAsync();
            expiry.Dispose();
        }

        book.Disconnect(Context.ConnectionId);
        logger.LogInformation(
            "Map hub connections {Connections} subscribed cells {Cells}.",
            book.ConnectionCount,
            book.SubscribedCellCount);
        await base.OnDisconnectedAsync(exception);
    }

    public Task SubscribeCells(string[]? cellIds) => ChangeAsync(cellIds, subscribe: true);

    public Task UnsubscribeCells(string[]? cellIds) => ChangeAsync(cellIds, subscribe: false);

    private async Task ChangeAsync(string[]? cellIds, bool subscribe)
    {
        if (Context.Items[LimitsKey] is not MapRealtimeView view)
        {
            Reject("map.config_unavailable");
            return;
        }

        var limits = new MapSubscriptionLimits(view.CellDegrees, view.MaxCells, view.SubscribeCallsPerMinute);
        var decision = subscribe
            ? book.Subscribe(Context.ConnectionId, cellIds, clock.UtcNow, limits)
            : book.Unsubscribe(Context.ConnectionId, cellIds, limits);
        if (decision.Close)
        {
            Reject(decision.Reason ?? "invalid_cell");
            return;
        }

        foreach (var cellId in decision.Added)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, cellId);
        }

        foreach (var cellId in decision.Removed)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, cellId);
        }
    }

    private void Reject(string reason)
    {
        logger.LogInformation("Map hub closed a connection ({Reason}).", reason);
        Context.Abort();
        throw new HubException(CloseMessage(reason));
    }

    private static string CloseMessage(string reason) => reason switch
    {
        "too_many_cells" => "Demasiadas zonas abiertas a la vez.",
        "subscribe_rate" => "Demasiados cambios de zona. Espera un momento.",
        "invalid_cell" => "La zona no es válida.",
        _ => "No se pudo abrir el mapa en tiempo real.",
    };

    private async Task CloseWhenExpiredAsync(HubCallerContext context, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var delay = expiresAt - clock.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        logger.LogInformation("Map hub closed a connection because the access token expired.");
        context.Abort();
    }
}

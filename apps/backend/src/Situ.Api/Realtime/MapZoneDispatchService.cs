using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.MapRealtime.PublishMapZoneNotices;

namespace Situ.Api.Realtime;

public sealed class MapZoneDispatchService(IServiceScopeFactory scopes, ILogger<MapZoneDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMilliseconds(1000);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<PublishMapZoneNoticesRequest, Result<PublishMapZoneNoticesResponse>>>();
                var result = await handler.Handle(new PublishMapZoneNoticesRequest(), stoppingToken);
                if (result.IsSuccess)
                {
                    delay = TimeSpan.FromMilliseconds(result.Value!.CoalesceIntervalMilliseconds);
                    if (result.Value.Published > 0)
                    {
                        logger.LogInformation(
                            "Map zone events emitted {Events} cells {Cells}.",
                            result.Value.Published,
                            result.Value.Cells);
                    }

                    if (result.Value.Failed > 0)
                    {
                        logger.LogWarning("Map zone publish failed for {Cells} cells.", result.Value.Failed);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Map zone publish failed ({ExceptionType}).", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

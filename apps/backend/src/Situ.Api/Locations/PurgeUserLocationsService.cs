using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Location;

namespace Situ.Api.Locations;

public sealed class PurgeUserLocationsService(IServiceScopeFactory scopes, ILogger<PurgeUserLocationsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<PurgeUserLocationsRequest, Result<PurgeUserLocationsResponse>>>();
                var result = await handler.Handle(new PurgeUserLocationsRequest(), stoppingToken);
                if (result.IsSuccess && result.Value!.Removed > 0)
                {
                    logger.LogInformation("Purged {Count} expired shared locations.", result.Value.Removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Shared location purge failed ({ExceptionType}).", exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

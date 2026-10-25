using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Notifications.Dispatch;
using Situ.Application.Features.Notifications.PurgeAlertLocations;

namespace Situ.Api.Notifications;

public sealed class NotificationDispatchService(IServiceScopeFactory scopes, ILogger<NotificationDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<ProcessNotificationJobsRequest, Result<ProcessNotificationJobsResponse>>>();
                var result = await handler.Handle(new ProcessNotificationJobsRequest(), stoppingToken);
                if (result.IsSuccess && result.Value!.Retried > 0)
                {
                    logger.LogWarning("Notification dispatch retried {Count} jobs.", result.Value.Retried);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Notification dispatch failed ({ExceptionType}).", exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class PurgeAlertLocationsService(IServiceScopeFactory scopes, ILogger<PurgeAlertLocationsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<PurgeAlertLocationsRequest, Result<PurgeAlertLocationsResponse>>>();
                await handler.Handle(new PurgeAlertLocationsRequest(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Alert location purge failed ({ExceptionType}).", exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

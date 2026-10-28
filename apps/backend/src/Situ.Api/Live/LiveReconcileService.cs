using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Live;

namespace Situ.Api.Live;

/// <summary>Polls the local MediaMTX control API. Recording deletion stays a later job.</summary>
public sealed class LiveReconcileService(IServiceScopeFactory scopes, ILogger<LiveReconcileService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<ReconcileLiveRequest, Result<ReconcileLiveResponse>>>();
                await handler.Handle(new ReconcileLiveRequest(), stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Live reconcile failed ({ExceptionType}).", exception.GetType().Name);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

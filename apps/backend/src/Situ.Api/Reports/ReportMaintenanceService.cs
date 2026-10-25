using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports.ExpireReports;
using Situ.Application.Features.Reports.PurgePendingReportMedia;
using Situ.Application.Features.Reports.RetainReports;

namespace Situ.Api.Reports;

public sealed class ExpireReportsService(IServiceScopeFactory scopes, ILogger<ExpireReportsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<ExpireReportsRequest, Result<ExpireReportsResponse>>>();
                await handler.Handle(new ExpireReportsRequest(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Report expiry failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class PurgePendingReportMediaService(IServiceScopeFactory scopes, ILogger<PurgePendingReportMediaService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<PurgePendingReportMediaRequest, Result<PurgePendingReportMediaResponse>>>();
                await handler.Handle(new PurgePendingReportMediaRequest(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Pending report media purge failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class RetainReportsService(IServiceScopeFactory scopes, ILogger<RetainReportsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<RetainReportsRequest, Result<RetainReportsResponse>>>();
                await handler.Handle(new RetainReportsRequest(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Report retention failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

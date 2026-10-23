using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.ExpireGuardianConsents;

namespace Situ.Api.Guardianship;

/// <summary>Marks guardian requests as expired once their 7-day response window has passed.</summary>
public sealed class GuardianConsentExpiryService(
    IServiceScopeFactory scopes,
    ILogger<GuardianConsentExpiryService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<IHandler<ExpireGuardianConsentsRequest, Result<ExpireGuardianConsentsResponse>>>();
                await handler.Handle(new ExpireGuardianConsentsRequest(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Guardian consent expiry failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

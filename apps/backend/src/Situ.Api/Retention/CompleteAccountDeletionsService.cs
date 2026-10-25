using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction.AccountDeletion;

namespace Situ.Api.Retention;

public sealed class CompleteAccountDeletionsService(IServiceScopeFactory scopes, ILogger<CompleteAccountDeletionsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<CompleteAccountDeletionsRequest, Result<CompleteAccountDeletionsResponse>>>();
                var result = await handler.Handle(new CompleteAccountDeletionsRequest(), stoppingToken);
                if (result.IsSuccess && result.Value!.BlockedByLegalHold > 0)
                {
                    logger.LogWarning(
                        "Account deletion blocked by legal hold for {Count} accounts. Staff should review the audit log.",
                        result.Value.BlockedByLegalHold);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Account deletion job failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Situ.Infrastructure.Reports;

namespace Situ.Api.Health;

public sealed class StorageHealthCheck(FileStorageOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.Root))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy());
            }

            Directory.CreateDirectory(options.Root);
            var probe = Path.Combine(options.Root, ".health");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }
}

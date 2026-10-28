using Microsoft.Extensions.Diagnostics.HealthChecks;
using Situ.Infrastructure.Reports;

namespace Situ.Api.Health;

public sealed class SkiaHealthCheck : IHealthCheck
{
    private readonly Action _probe;

    public SkiaHealthCheck()
        : this(SkiaNativeProbe.EnsureLoaded)
    {
    }

    public SkiaHealthCheck(Action probe)
    {
        _probe = probe;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _probe();
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }
}

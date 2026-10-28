using Microsoft.Extensions.Diagnostics.HealthChecks;
using Situ.Api.Health;
using Situ.Infrastructure.Reports;

namespace Situ.Architecture.Tests;

public class SkiaNativeTests
{
    [Fact]
    public async Task Ready_check_loads_skia_and_stays_silent_when_the_library_is_missing()
    {
        SkiaNativeProbe.EnsureLoaded();

        var healthy = await new SkiaHealthCheck().CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, healthy.Status);
        Assert.Null(healthy.Description);
        Assert.Empty(healthy.Data);

        var missing = await new SkiaHealthCheck(() => throw new DllNotFoundException("libSkiaSharp"))
            .CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, missing.Status);
        Assert.Null(missing.Description);
        Assert.Empty(missing.Data);
    }

    [Fact]
    public void Linux_publish_uses_the_bundle_without_system_font_libraries_or_harfbuzz()
    {
        var root = FindRepoRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "apps/backend/src/Situ.Infrastructure/Situ.Infrastructure.csproj"));
        var props = File.ReadAllText(Path.Combine(root, "apps/backend/Directory.Packages.props"));
        Assert.Contains("Include=\"SkiaSharp.NativeAssets.Linux.NoDependencies\"", csproj, StringComparison.Ordinal);
        Assert.Contains("Include=\"SkiaSharp.NativeAssets.Linux.NoDependencies\"", props, StringComparison.Ordinal);
        Assert.DoesNotContain("Include=\"SkiaSharp.NativeAssets.Linux\"", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Include=\"HarfBuzz", csproj + props, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "apps/backend/Directory.Packages.props")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}

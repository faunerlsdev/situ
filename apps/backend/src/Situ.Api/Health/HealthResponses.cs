using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Situ.Api.Health;

public static class HealthResponses
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
            }),
        };

        return context.Response.WriteAsJsonAsync(body, JsonSerializerOptions.Web);
    }
}

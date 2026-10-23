using Microsoft.Extensions.Configuration;

namespace Situ.Infrastructure.Mail;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1025;

    public string FromAddress { get; init; } = "no-reply@situ.local";

    public string FromName { get; init; } = "SITU";

    /// <summary>Origin of the API, used to build the confirmation link that Mailpit can open.</summary>
    public string ApiPublicBaseUrl { get; init; } = "http://localhost:5080";

    public bool UseSsl { get; init; }

    public static EmailOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new EmailOptions
        {
            Host = string.IsNullOrWhiteSpace(section["Host"]) ? "localhost" : section["Host"]!.Trim(),
            Port = int.TryParse(section["Port"], out var port) ? port : 1025,
            FromAddress = string.IsNullOrWhiteSpace(section["FromAddress"]) ? "no-reply@situ.local" : section["FromAddress"]!.Trim(),
            FromName = string.IsNullOrWhiteSpace(section["FromName"]) ? "SITU" : section["FromName"]!.Trim(),
            ApiPublicBaseUrl = string.IsNullOrWhiteSpace(section["ApiPublicBaseUrl"])
                ? "http://localhost:5080"
                : section["ApiPublicBaseUrl"]!.Trim().TrimEnd('/'),
            UseSsl = bool.TryParse(section["UseSsl"], out var useSsl) && useSsl,
        };
    }
}

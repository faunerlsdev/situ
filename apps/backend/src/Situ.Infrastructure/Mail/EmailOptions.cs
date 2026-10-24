using Microsoft.Extensions.Configuration;

namespace Situ.Infrastructure.Mail;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1025;

    public string FromAddress { get; init; } = "no-reply@situ.local";

    public string FromName { get; init; } = "SITU";

    /// <summary>Origin of the API, used to build public links such as email confirmation.</summary>
    public string ApiPublicBaseUrl { get; init; } = "http://localhost:5080";

    /// <summary>Origin of the web app, used for the public guardian review page.</summary>
    public string WebPublicBaseUrl { get; init; } = "http://localhost:3000";

    public bool UseSsl { get; init; }

    public string Username { get; init; } = "";

    public string Password { get; init; } = "";

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
            WebPublicBaseUrl = string.IsNullOrWhiteSpace(section["WebPublicBaseUrl"])
                ? "http://localhost:3000"
                : section["WebPublicBaseUrl"]!.Trim().TrimEnd('/'),
            UseSsl = bool.TryParse(section["UseSsl"], out var useSsl) && useSsl,
            Username = section["Username"]?.Trim() ?? "",
            Password = section["Password"] ?? "",
        };
    }
}

namespace Situ.Api.Configuration;

/// <summary>Origin used to build absolute URLs for static assets. Falls back to Email:ApiPublicBaseUrl.</summary>
public sealed class PublicBaseUrlOptions
{
    public string Url { get; init; } = "http://localhost:5080";

    public static PublicBaseUrlOptions From(IConfiguration configuration)
    {
        var configured = configuration["PublicBaseUrl"];
        var email = configuration["Email:ApiPublicBaseUrl"];
        var url = string.IsNullOrWhiteSpace(configured) ? email : configured;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = "http://localhost:5080";
        }

        return new PublicBaseUrlOptions { Url = url.Trim().TrimEnd('/') };
    }
}

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Situ.Infrastructure.Captcha;

/// <summary>Accepts only the fixed development token. Never use it outside Development.</summary>
public sealed class DevelopmentCaptchaVerifier : Application.Common.Abstractions.ICaptchaVerifier
{
    public const string AcceptedToken = "dev-ok";

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(token, AcceptedToken, StringComparison.Ordinal));
}

/// <summary>Cloudflare Turnstile. An empty secret or a failed request rejects the token.</summary>
public sealed class TurnstileCaptchaVerifier(HttpClient http, IConfiguration configuration) : Application.Common.Abstractions.ICaptchaVerifier
{
    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        var secret = configuration["Captcha:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            using var response = await http.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secret,
                    ["response"] = token,
                    ["remoteip"] = remoteIp ?? string.Empty,
                }),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var payload = await response.Content.ReadFromJsonAsync<TurnstileResult>(cancellationToken);
            return payload?.Success == true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private sealed record TurnstileResult([property: JsonPropertyName("success")] bool Success);
}

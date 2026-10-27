namespace Situ.Application.Common.Abstractions;

/// <summary>
/// Checks a captcha token on the server. A missing verifier, an empty secret or a network failure
/// must return false (fail closed). The secret never leaves the server.
/// </summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}

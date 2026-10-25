using Situ.Application.Common.Abstractions;

namespace Situ.Infrastructure.Mail;

public sealed class PublicLinks(EmailOptions options) : IPublicLinks
{
    public string EmailConfirmation(string plainToken)
    {
        var root = options.ApiPublicBaseUrl.TrimEnd('/');
        return $"{root}/api/v1/auth/confirm-email?token={Uri.EscapeDataString(plainToken)}";
    }

    public string PasswordReset(string plainToken)
    {
        var root = options.ApiPublicBaseUrl.TrimEnd('/');
        return $"{root}/api/v1/auth/password-reset?token={Uri.EscapeDataString(plainToken)}";
    }

    public string GuardianEmailVerification(string plainToken)
    {
        var root = options.ApiPublicBaseUrl.TrimEnd('/');
        return $"{root}/api/v1/guardianship/consent/verify-email?token={Uri.EscapeDataString(plainToken)}";
    }

    public string GuardianReview(string plainToken)
    {
        var root = options.WebPublicBaseUrl.TrimEnd('/');
        return $"{root}/tutor/autorizar?token={Uri.EscapeDataString(plainToken)}";
    }

    public string GuardianManage(string plainToken)
    {
        var root = options.WebPublicBaseUrl.TrimEnd('/');
        return $"{root}/tutor/gestionar?token={Uri.EscapeDataString(plainToken)}";
    }

    public string Absolute(string path)
    {
        var relative = path.StartsWith('/') ? path : "/" + path;
        return options.ApiPublicBaseUrl.TrimEnd('/') + relative;
    }
}

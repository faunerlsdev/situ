using System.Security.Cryptography;
using System.Text;
using Situ.Application.Common.Abstractions;

namespace Situ.Infrastructure.Guardianship;

public sealed class HmacGuardianConsentSigner(GuardianConsentOptions options) : IGuardianConsentSigner
{
    public string Sign(GuardianConsentSignature input)
    {
        options.EnsureValid();
        var payload = string.Join(
            '\n',
            input.ConsentId.ToString("D"),
            input.MinorUserId.ToString("D"),
            input.NoticeVersion,
            input.GuardianFullName,
            input.DocumentType,
            input.DocumentNumber,
            input.ContactEmail,
            input.LiveStreamingAuthorized ? "1" : "0",
            input.LocationSharingAuthorized ? "1" : "0",
            input.AuthorizedAt.ToUniversalTime().ToString("O"));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKey), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}

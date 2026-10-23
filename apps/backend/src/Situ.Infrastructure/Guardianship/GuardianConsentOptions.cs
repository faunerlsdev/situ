using System.Text;
using Microsoft.Extensions.Configuration;

namespace Situ.Infrastructure.Guardianship;

public sealed class GuardianConsentOptions
{
    public const string SectionName = "GuardianConsent";

    public string SigningKey { get; init; } = "";

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException("GuardianConsent:SigningKey must be at least 32 bytes.");
        }
    }

    public static GuardianConsentOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new GuardianConsentOptions
        {
            SigningKey = section["SigningKey"]?.Trim() ?? "",
        };
    }
}

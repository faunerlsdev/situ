using System.Text;

namespace Situ.Infrastructure.Guardianship;

public sealed class GuardianConsentOptions
{
    public const string SectionName = "GuardianConsent";

    /// <summary>
    /// Fixed key used only when the host environment is Development and no key was configured.
    /// Signatures produced with it are not valid.
    /// </summary>
    public const string DevelopmentSigningKey = "situ-dev-guardian-consent-signing-key";

    public const string MissingSigningKeyMessage =
        "Falta GuardianConsent:SigningKey. Defínela con la variable de entorno GuardianConsent__SigningKey";

    public const string DevelopmentSigningKeyWarning =
        "GuardianConsent:SigningKey no está configurada. Se usa la clave de desarrollo; las firmas generadas no tienen validez.";

    public string SigningKey { get; set; } = "";

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException(MissingSigningKeyMessage);
        }
    }
}

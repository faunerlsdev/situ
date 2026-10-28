namespace Situ.Architecture.Tests;

internal static class ProductionTestConfiguration
{
    public static void Apply(IDictionary<string, string?> values)
    {
        values["FileStorage:Root"] = Path.GetTempPath();
        values["FileStorage:SigningKey"] = "file-signing-key-for-tests-32b-minimum";
        values["Notifications:RequireExpoAccessToken"] = "true";
        values["Notifications:ExpoAccessToken"] = "ExponentPushToken-test-value-32chars";
        values["Captcha:Provider"] = "Turnstile";
        values["Captcha:SecretKey"] = "turnstile-secret-for-tests";
        values["Captcha:SiteKey"] = "turnstile-site-for-tests";
        values["PublicBaseUrl"] = "https://api.example.test";
        values["Email:Host"] = "smtp.example.test";
        values["Email:Port"] = "587";
        values["Email:FromAddress"] = "no-reply@example.test";
        values["Email:FromName"] = "SITU";
        values["Email:UseSsl"] = "true";
        values["Email:Username"] = "situ";
        values["Email:Password"] = "smtp-credential-for-tests";
        values["Email:ApiPublicBaseUrl"] = "https://api.example.test";
        values["Email:WebPublicBaseUrl"] = "https://app.example.test";
        values["Cors:AllowedOrigins:0"] = "https://app.example.test";
        values["Cors:AllowedOrigins:1"] = "https://admin.example.test";
        values["Cors:AllowedOrigins:2"] = "https://ayuda.example.test";
        values["Live:IngestSecret"] = "live-ingest-secret-for-tests-32b";
        values["Live:HlsSecret"] = "live-hls-secret-for-tests-32chars";
    }
}

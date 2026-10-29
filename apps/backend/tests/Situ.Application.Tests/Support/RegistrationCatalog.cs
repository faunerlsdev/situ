using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity.RegisterUser;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Legal;

namespace Situ.Application.Tests.Support;

internal sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Messages { get; } = [];

    public Exception? Failure { get; set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Messages.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

internal sealed class FakeLinks : IPublicLinks
{
    public string EmailConfirmation(string plainToken) =>
        $"https://situ.test/api/v1/auth/confirm-email?token={Uri.EscapeDataString(plainToken)}";

    public string PasswordReset(string plainToken) =>
        $"https://situ.test/api/v1/auth/password-reset?token={Uri.EscapeDataString(plainToken)}";

    public string GuardianEmailVerification(string plainToken) =>
        $"https://situ.test/api/v1/guardianship/consent/verify-email?token={Uri.EscapeDataString(plainToken)}";

    public string GuardianReview(string plainToken) =>
        $"https://situ.test/tutor/autorizar?token={Uri.EscapeDataString(plainToken)}";

    public string GuardianManage(string plainToken) =>
        $"https://situ.test/tutor/gestionar?token={Uri.EscapeDataString(plainToken)}";

    public string FriendInvitation(string plainToken) =>
        $"https://situ.test/amigos/invitacion?token={Uri.EscapeDataString(plainToken)}";

    public string Absolute(string path) =>
        "https://situ.test" + (path.StartsWith('/') ? path : "/" + path);
}

internal sealed class AcceptCaptcha(bool ok) : ICaptchaVerifier
{
    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) =>
        Task.FromResult(ok && token == "dev-ok");
}

internal sealed class FakePasswords : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
}

internal static class RegistrationCatalog
{
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);

    public static async Task<Catalog> SeedAsync(IdentityTestDb db)
    {
        var country = Country.Create(
            "CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
        var region = Region.Create(country.Id, "Cundinamarca", "25");
        var city = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
        var otherRegion = Region.Create(country.Id, "Antioquia", "05");
        var otherCity = City.Create(otherRegion.Id, country.Id, "Medellín", new GeoPoint(6.244, -75.581), "America/Bogota");
        var terms = LegalDocument.Publish(
            LegalDocumentType.TermsOfUse, country.Id, "es-CO", "2026-10-29", "https://somossitu.com/terminos", Now);
        var privacy = LegalDocument.Publish(
            LegalDocumentType.PrivacyPolicy, country.Id, "es-CO", "2026-10-29", "https://somossitu.com/privacidad", Now);

        db.AddRange(country, region, city, otherRegion, otherCity, terms, privacy);
        await db.SaveChangesAsync();

        return new Catalog(country.Id, region.Id, city.Id, otherCity.Id, [terms.Id, privacy.Id]);
    }

    public static RegisterUserRequest Request(Catalog catalog, int ageYears, string? password = null, string email = "ana@situ.test")
    {
        var birth = DateOnly.FromDateTime(Now.UtcDateTime).AddYears(-ageYears);
        return new RegisterUserRequest(
            email,
            "ana",
            "Ana",
            "Pérez",
            "+573001234567",
            password ?? "Clave123",
            birth,
            catalog.CountryId,
            catalog.RegionId,
            catalog.CityId,
            catalog.DocumentIds,
            "127.0.0.1",
            "tests");
    }

    public static string TokenFrom(string body)
    {
        const string marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, body);
        var value = body[(start + marker.Length)..].Split(['\r', '\n', ' ', '"', '\'', '<', '>'], 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return Uri.UnescapeDataString(value);
    }
}

internal sealed record Catalog(Guid CountryId, Guid RegionId, Guid CityId, Guid OtherCityId, IReadOnlyList<Guid> DocumentIds);

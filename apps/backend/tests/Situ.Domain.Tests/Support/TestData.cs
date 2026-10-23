using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Domain.Tests.Support;

internal static class TestData
{
    public static DateTimeOffset Now { get; } = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);

    public static User UserAged(int years, int minimumAge = 14)
    {
        var birthDate = DateOnly.FromDateTime(Now.UtcDateTime).AddYears(-years);
        return User.Register(
            Email.Create($"user{years}@situ.test"),
            UserName.Create($"user{years}"),
            "hash",
            birthDate,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "es-CO",
            minimumAge,
            Now);
    }
}

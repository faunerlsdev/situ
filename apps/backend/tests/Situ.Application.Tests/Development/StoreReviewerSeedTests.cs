using Microsoft.EntityFrameworkCore;
using Situ.Application.Development;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Tests.Development;

public class StoreReviewerSeedTests
{
    [Fact]
    public async Task Creates_an_active_adult_without_two_factor_and_replaces_the_password()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await RegistrationCatalog.SeedAsync(db);
        var passwords = new FakePasswords();

        var created = await StoreReviewerSeed.ApplyAsync(db, passwords, clock, CancellationToken.None);
        var again = await StoreReviewerSeed.ApplyAsync(db, passwords, clock, CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.NotEqual(created.Value, again.Value);
        Assert.True(PasswordPolicy.IsValid(created.Value));
        var user = await db.Users.SingleAsync(item => item.Email == Email.Create(StoreReviewerSeed.EmailAddress));
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.False(user.TwoFactorEnabled);
        Assert.False(user.IsMinor(clock.UtcNow));
        Assert.NotNull(user.EmailConfirmedAt);
        Assert.True(passwords.Verify(again.Value!, user.PasswordHash));
    }
}

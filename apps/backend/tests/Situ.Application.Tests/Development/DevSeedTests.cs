using Microsoft.EntityFrameworkCore;
using Situ.Application.Development;
using Situ.Application.Tests.Support;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Domain.Reports;

namespace Situ.Application.Tests.Development;

public class DevSeedTests
{
    [Fact]
    public async Task Seed_is_idempotent_and_clean_removes_only_the_sample()
    {
        await using var db = IdentityTestDb.Create();
        await RegistrationCatalog.SeedAsync(db);
        foreach (var (code, index) in DevSeed.CategoryCodes.Select((code, index) => (code, index)))
        {
            db.ReportCategories.Add(ReportCategory.Create(code, code, "Ejemplo de la categoría.", "/b.png", "/m.png", "#112233", index, 120, 1));
        }

        await db.SaveChangesAsync();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var passwords = new FakePasswords();

        var seeded = await DevSeed.ApplyAsync(db, passwords, clock, isDevelopment: true, clean: false, CancellationToken.None);
        var again = await DevSeed.ApplyAsync(db, passwords, clock, isDevelopment: true, clean: false, CancellationToken.None);

        Assert.True(seeded.IsSuccess);
        Assert.Equal(seeded.Value, again.Value);
        Assert.Equal(new DevSeedResult(3, 8, 3, 4, 1), seeded.Value);
        var users = await db.Users.ToListAsync();
        Assert.Equal(3, users.Count);
        Assert.All(users, user =>
        {
            Assert.Equal(UserStatus.Active, user.Status);
            Assert.False(user.IsMinor(RegistrationCatalog.Now));
            Assert.NotEqual(DevSeed.Password, user.PasswordHash);
            Assert.True(passwords.Verify(DevSeed.Password, user.PasswordHash));
        });
        Assert.Equal(DevSeed.Users.Select(user => user.Email).OrderBy(email => email), users.Select(user => user.Email.Value).OrderBy(email => email));

        var reports = await db.Reports.ToListAsync();
        Assert.Equal(8, reports.Select(report => report.CategoryId).Distinct().Count());
        Assert.Contains(reports, report => report.IsAnonymous);
        Assert.Contains(reports, report => !report.IsAnonymous);
        Assert.All(reports, report =>
        {
            Assert.StartsWith(DevSeed.Marker, report.Description);
            Assert.InRange(report.Location.Latitude, 4.68, 4.75);
            Assert.InRange(report.Location.Longitude, -74.11, -74.03);
        });
        var comments = await db.Comments.ToListAsync();
        Assert.Contains(comments, comment => comment.IsAnonymous);
        Assert.Contains(comments, comment => !comment.IsAnonymous);
        var complaints = await db.Complaints.ToListAsync();
        Assert.All(complaints, complaint => Assert.Equal(ComplaintStatus.Open, complaint.Status));
        Assert.Contains(complaints, complaint => complaint.IsPriority);
        Assert.Contains(complaints, complaint => !complaint.IsPriority);
        var anonymousIds = reports.Where(report => report.IsAnonymous).Select(report => report.Id).ToHashSet();
        Assert.Equal(2, complaints.Count(complaint => anonymousIds.Contains(complaint.TargetId)));
        var takedown = await db.TakedownRequests.SingleAsync();
        Assert.Equal(TakedownStatus.Received, takedown.Status);
        Assert.Equal(RegistrationCatalog.Now.Add(DevSeed.TakedownDueIn), takedown.DueAt);

        var blocked = await DevSeed.ApplyAsync(db, passwords, clock, isDevelopment: false, clean: true, CancellationToken.None);
        Assert.Equal("dev_seed.forbidden", blocked.Error!.Code);
        Assert.Equal(8, await db.Reports.CountAsync());

        var cleaned = await DevSeed.ApplyAsync(db, passwords, clock, isDevelopment: true, clean: true, CancellationToken.None);
        Assert.True(cleaned.IsSuccess);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Reports.CountAsync());
        Assert.Equal(0, await db.Comments.CountAsync());
        Assert.Equal(0, await db.Complaints.CountAsync());
        Assert.Equal(0, await db.TakedownRequests.CountAsync());
        Assert.Equal(0, await db.UserProfiles.CountAsync());

        var restored = await DevSeed.ApplyAsync(db, passwords, clock, isDevelopment: true, clean: false, CancellationToken.None);
        Assert.Equal(new DevSeedResult(3, 8, 3, 4, 1), restored.Value);
    }

    [Fact]
    public async Task Seed_refuses_outside_development_and_without_bogota()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var passwords = new FakePasswords();

        var denied = await DevSeed.ApplyAsync(db, passwords, clock, isDevelopment: false, clean: false, CancellationToken.None);
        var missingCity = await DevSeed.ApplyAsync(db, passwords, clock, isDevelopment: true, clean: false, CancellationToken.None);

        Assert.Equal("dev_seed.forbidden", denied.Error!.Code);
        Assert.Equal("dev_seed.city", missingCity.Error!.Code);
        Assert.Equal(0, await db.Users.CountAsync());
    }
}

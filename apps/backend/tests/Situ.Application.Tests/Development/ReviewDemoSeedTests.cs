using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Development;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Legal;
using Situ.Domain.Live;
using Situ.Domain.Reports;

namespace Situ.Application.Tests.Development;

public class ReviewDemoSeedTests
{
    [Fact]
    public async Task Creates_two_accounts_and_sample_content_once()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await RegistrationCatalog.SeedAsync(db);
        db.ReportCategories.Add(ReportCategory.Create(
            "trafico",
            "Tráfico",
            "Tráfico",
            "/assets/categories/trafico.png",
            "/assets/categories/trafico.png",
            "#336699",
            1,
            120,
            1));
        await db.SaveChangesAsync();
        var files = new MemoryFiles();
        const string reviewPassword = "Revision1";
        const string deletePassword = "Borrado1";

        var created = await ReviewDemoSeed.ApplyAsync(db, new FakePasswords(), files, clock, reviewPassword, deletePassword, CancellationToken.None);
        var again = await ReviewDemoSeed.ApplyAsync(db, new FakePasswords(), files, clock, reviewPassword, deletePassword, CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(3, await db.Reports.CountAsync());
        Assert.Equal(3, await db.ReportMedias.CountAsync());
        var stream = await db.LiveStreams.SingleAsync();
        Assert.Equal(LiveStreamStatus.Ended, stream.Status);
        Assert.Equal("review-demo/live/recording.bin", stream.RecordingKey);
        Assert.Equal(1, await db.Comments.CountAsync());
        Assert.Equal(3, files.Saved.Count(item => item.EndsWith(".jpg", StringComparison.Ordinal)));
        var review = await db.Users.SingleAsync(item => item.Email == Email.Create(ReviewDemoSeed.ReviewEmailAddress));
        var deletion = await db.Users.SingleAsync(item => item.Email == Email.Create(ReviewDemoSeed.DeleteEmailAddress));
        Assert.Equal(UserStatus.Active, review.Status);
        Assert.Equal(UserStatus.Active, deletion.Status);
        Assert.NotNull(review.EmailConfirmedAt);
        Assert.NotNull(deletion.EmailConfirmedAt);
        Assert.False(review.IsMinor(clock.UtcNow));
        Assert.False(deletion.IsMinor(clock.UtcNow));
        Assert.Equal(1, await db.ConsentRecords.CountAsync(item => item.UserId == review.Id && item.DocumentType == LegalDocumentType.TermsOfUse && item.Version == "2026-10-29"));
        Assert.Equal(1, await db.ConsentRecords.CountAsync(item => item.UserId == deletion.Id && item.DocumentType == LegalDocumentType.TermsOfUse && item.Version == "2026-10-29"));
    }

    private sealed class MemoryFiles : IFileStorage
    {
        public List<string> Saved { get; } = [];

        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
        {
            Saved.Add(key);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
    }
}

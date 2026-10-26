using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;

namespace Situ.Application.Development;

/// <summary>
/// Development-only sample accounts and complaint-queue content. Rows are marked with <see cref="Marker"/>
/// and the three fixed emails so <c>--clean</c> can remove them. The takedown is filed in the past so its
/// domain deadline (15 days) falls inside the dashboard's two-day "due soon" window.
/// </summary>
public static class DevSeed
{
    public const string Marker = "[dev-seed]";
    public const string Password = "Semilla123";
    public static readonly TimeSpan TakedownDueIn = TimeSpan.FromHours(12);

    public static readonly IReadOnlyList<SampleUser> Users =
    [
        new("ana.ejemplo@situ.test", "ana_ejemplo", "Ana", "Ejemplo", "3001000001"),
        new("bruno.ejemplo@situ.test", "bruno_ejemplo", "Bruno", "Ejemplo", "3001000002"),
        new("carla.ejemplo@situ.test", "carla_ejemplo", "Carla", "Ejemplo", "3001000003"),
    ];

    private static readonly SampleReport[] Samples =
    [
        new("accidente_transito", false, null, true),
        new("hurto", false, ComplaintReason.Spam, false),
        new("puesto_control", true, ComplaintReason.Harassment, true),
        new("basuras", true, null, false),
        new("via_cerrada", false, ComplaintReason.Violence, false),
        new("derrumbe", true, ComplaintReason.MinorSafety, false),
        new("inundacion", false, null, false),
        new("manifestacion", false, null, true),
    ];

    public static IReadOnlyList<string> CategoryCodes { get; } = Samples.Select(sample => sample.CategoryCode).ToArray();

    public static async Task<Result<DevSeedResult>> ApplyAsync(
        IApplicationDbContext db,
        IPasswordHasher passwords,
        IClock clock,
        bool isDevelopment,
        bool clean,
        CancellationToken cancellationToken)
    {
        if (!isDevelopment)
        {
            return Result<DevSeedResult>.Failure(Error.Forbidden("dev_seed.forbidden", "dev:seed solo corre en Development."));
        }

        if (!PasswordPolicy.IsValid(Password))
        {
            return Result<DevSeedResult>.Failure(Error.Failure("dev_seed.password", "La contraseña de ejemplo no cumple la política."));
        }

        if (clean)
        {
            var removed = await RemoveAsync(db, cancellationToken);
            if (removed.IsFailure)
            {
                return Result<DevSeedResult>.Failure(removed.Error!);
            }
        }

        if (clean)
        {
            return Result<DevSeedResult>.Success(new DevSeedResult(0, 0, 0, 0, 0));
        }

        var city = await db.Cities.FirstOrDefaultAsync(item => item.Name == "Bogotá", cancellationToken);
        if (city is null)
        {
            return Result<DevSeedResult>.Failure(Error.NotFound("dev_seed.city", "No está la ciudad Bogotá. Corre las migraciones antes de dev:seed."));
        }

        var country = await db.Countries.FirstAsync(item => item.Id == city.CountryId, cancellationToken);
        var categories = await db.ReportCategories.Where(item => CategoryCodes.Contains(item.Code)).ToListAsync(cancellationToken);
        var missing = CategoryCodes.FirstOrDefault(code => categories.All(item => item.Code != code));
        if (missing is not null)
        {
            return Result<DevSeedResult>.Failure(Error.NotFound("dev_seed.category", $"No está la categoría {missing}. Corre las migraciones antes de dev:seed."));
        }

        var now = clock.UtcNow;
        var people = new List<User>(Users.Count);
        foreach (var sample in Users)
        {
            people.Add(await EnsureUserAsync(db, passwords, sample, city, country, now, cancellationToken));
        }

        var reports = new List<Report>(Samples.Length);
        for (var index = 0; index < Samples.Length; index++)
        {
            var sample = Samples[index];
            var category = categories.Single(item => item.Code == sample.CategoryCode);
            var author = people[index % people.Count];
            var report = await EnsureReportAsync(db, sample, category, author, city, now, index, cancellationToken);
            reports.Add(report);
            if (sample.Comment)
            {
                await EnsureCommentAsync(db, sample, report, author, now, cancellationToken);
            }

            if (sample.Complaint is { } reason)
            {
                var reporter = people[(index + 1) % people.Count];
                await EnsureComplaintAsync(db, sample, report, reporter, reason, now, cancellationToken);
            }
        }

        await EnsureTakedownAsync(db, reports[0], now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var markedReports = await db.Reports.CountAsync(item => item.Description.StartsWith(Marker), cancellationToken);
        var markedComments = await db.Comments.CountAsync(item => item.Text.StartsWith(Marker), cancellationToken);
        var markedComplaints = await db.Complaints.CountAsync(item => item.Details != null && item.Details.StartsWith(Marker), cancellationToken);
        var markedTakedowns = await db.TakedownRequests.CountAsync(item => item.Description.StartsWith(Marker), cancellationToken);
        return Result<DevSeedResult>.Success(new DevSeedResult(people.Count, markedReports, markedComments, markedComplaints, markedTakedowns));
    }

    private static async Task<User> EnsureUserAsync(
        IApplicationDbContext db,
        IPasswordHasher passwords,
        SampleUser sample,
        City city,
        Country country,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(sample.Email);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (user is null)
        {
            user = User.Register(
                email,
                UserName.Create(sample.UserName),
                PersonName.Create(sample.GivenNames),
                PersonName.Create(sample.Surnames),
                PhoneNumber.Create(sample.Phone),
                passwords.Hash(Password),
                new DateOnly(1990, 5, 15),
                country.Id,
                city.RegionId,
                city.Id,
                "es-CO",
                country.MinimumAge,
                now);
            user.ConfirmEmail(now);
            db.Users.Add(user);
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (profile is null)
        {
            db.UserProfiles.Add(UserProfile.CreateDefault(user.Id, sample.GivenNames + " " + sample.Surnames));
        }

        return user;
    }

    private static async Task<Report> EnsureReportAsync(
        IApplicationDbContext db,
        SampleReport sample,
        ReportCategory category,
        User author,
        City city,
        DateTimeOffset now,
        int index,
        CancellationToken cancellationToken)
    {
        var description = Mark(sample.CategoryCode);
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Description == description, cancellationToken);
        if (report is not null)
        {
            return report;
        }

        report = Report.Create(
            author.Id,
            category.Id,
            city.Id,
            NearBogota(index),
            description,
            sample.Anonymous,
            authorIsMinor: false,
            category.EffectiveLifetime(),
            now);
        db.Reports.Add(report);
        return report;
    }

    private static async Task EnsureCommentAsync(
        IApplicationDbContext db,
        SampleReport sample,
        Report report,
        User author,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var text = Mark("comentario " + sample.CategoryCode);
        if (await db.Comments.AnyAsync(item => item.Text == text, cancellationToken))
        {
            return;
        }

        db.Comments.Add(Comment.Create(
            ContentTargetType.Report,
            report.Id,
            author.Id,
            text,
            sample.Anonymous,
            sample.Anonymous ? 1 : null,
            null,
            report.AuthorId,
            now));
        report.AddComment();
    }

    private static async Task EnsureComplaintAsync(
        IApplicationDbContext db,
        SampleReport sample,
        Report report,
        User reporter,
        ComplaintReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var details = Mark("denuncia " + reason.ToString().ToLowerInvariant() + " " + sample.CategoryCode);
        if (await db.Complaints.AnyAsync(item => item.Details == details, cancellationToken))
        {
            return;
        }

        db.Complaints.Add(Complaint.File(reporter.Id, ContentTargetType.Report, report.Id, reason, details, now));
    }

    private static async Task EnsureTakedownAsync(
        IApplicationDbContext db,
        Report report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var description = Mark("retiro " + Samples[0].CategoryCode);
        var existing = await db.TakedownRequests.FirstOrDefaultAsync(item => item.Description == description, cancellationToken);
        if (existing is not null && existing.Status == TakedownStatus.Received && existing.DueAt > now && existing.DueAt <= now.AddDays(2))
        {
            return;
        }

        if (existing is not null)
        {
            db.TakedownRequests.Remove(existing);
        }

        var filedAt = now.Add(TakedownDueIn).Subtract(TakedownRequest.DefaultResponseWindow);
        db.TakedownRequests.Add(TakedownRequest.Receive(
            "Persona de ejemplo",
            "retiro.ejemplo@situ.test",
            ContentTargetType.Report,
            report.Id,
            description,
            filedAt));
    }

    private static async Task<Result> RemoveAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var people = new List<User>();
            foreach (var sample in Users)
            {
                var email = Email.Create(sample.Email);
                var person = await db.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
                if (person is not null)
                {
                    people.Add(person);
                }
            }

            var userIds = people.Select(person => person.Id).ToArray();
            var reports = await MarkedReports(db, userIds, cancellationToken);
            var reportIds = reports.Select(item => item.Id).ToArray();
            var comments = await MarkedComments(db, userIds, reportIds, cancellationToken);
            var commentIds = comments.Select(item => item.Id).ToArray();
            var complaints = await MarkedComplaints(db, userIds, reportIds, commentIds, cancellationToken);
            var takedowns = await MarkedTakedowns(db, reportIds, commentIds, cancellationToken);
            var subjects = reportIds.Concat(commentIds).ToArray();
            var jobs = subjects.Length == 0
                ? []
                : await db.NotificationJobs.Where(item => subjects.Contains(item.SubjectId)).ToListAsync(cancellationToken);
            var confirmations = reportIds.Length == 0
                ? []
                : await db.ReportConfirmations.Where(item => reportIds.Contains(item.ReportId)).ToListAsync(cancellationToken);
            var reactions = subjects.Length == 0
                ? []
                : await db.Reactions.Where(item => subjects.Contains(item.TargetId)).ToListAsync(cancellationToken);

            db.NotificationJobs.RemoveRange(jobs);
            db.ReportConfirmations.RemoveRange(confirmations);
            db.Reactions.RemoveRange(reactions);
            db.Complaints.RemoveRange(complaints);
            db.TakedownRequests.RemoveRange(takedowns);
            db.Comments.RemoveRange(comments);
            await db.SaveChangesAsync(cancellationToken);

            db.Reports.RemoveRange(reports);
            await db.SaveChangesAsync(cancellationToken);

            if (userIds.Length > 0)
            {
                var profiles = await db.UserProfiles.Where(item => userIds.Contains(item.UserId)).ToListAsync(cancellationToken);
                var devices = await db.UserDevices.Where(item => userIds.Contains(item.UserId)).ToListAsync(cancellationToken);
                var tokens = await db.RefreshTokens.Where(item => userIds.Contains(item.UserId)).ToListAsync(cancellationToken);
                var recovery = await db.TwoFactorRecoveryCodes.Where(item => userIds.Contains(item.UserId)).ToListAsync(cancellationToken);
                var security = await db.SecurityTokens.Where(item => userIds.Contains(item.UserId)).ToListAsync(cancellationToken);
                var attempts = await db.LoginAttempts.Where(item => item.UserId != null && userIds.Contains(item.UserId.Value)).ToListAsync(cancellationToken);
                db.UserProfiles.RemoveRange(profiles);
                db.UserDevices.RemoveRange(devices);
                db.RefreshTokens.RemoveRange(tokens);
                db.TwoFactorRecoveryCodes.RemoveRange(recovery);
                db.SecurityTokens.RemoveRange(security);
                db.LoginAttempts.RemoveRange(attempts);
                await db.SaveChangesAsync(cancellationToken);
                db.Users.RemoveRange(people);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
        catch (DbUpdateException)
        {
            return Result.Failure(Error.Conflict(
                "dev_seed.clean_blocked",
                "No se pudieron borrar los datos de ejemplo porque alguna fila ajena todavía los referencia."));
        }
    }

    private static async Task<List<Report>> MarkedReports(IApplicationDbContext db, Guid[] userIds, CancellationToken cancellationToken)
    {
        if (userIds.Length == 0)
        {
            return await db.Reports.Where(item => item.Description.StartsWith(Marker)).ToListAsync(cancellationToken);
        }

        return await db.Reports
            .Where(item => item.Description.StartsWith(Marker) || userIds.Contains(item.AuthorId))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<Comment>> MarkedComments(
        IApplicationDbContext db,
        Guid[] userIds,
        Guid[] reportIds,
        CancellationToken cancellationToken)
    {
        var marked = await db.Comments.Where(item => item.Text.StartsWith(Marker)).ToListAsync(cancellationToken);
        var related = new List<Comment>();
        if (reportIds.Length > 0)
        {
            related.AddRange(await db.Comments.Where(item => reportIds.Contains(item.TargetId)).ToListAsync(cancellationToken));
        }

        if (userIds.Length > 0)
        {
            related.AddRange(await db.Comments.Where(item => userIds.Contains(item.AuthorId)).ToListAsync(cancellationToken));
        }

        return marked.Concat(related).DistinctBy(item => item.Id).ToList();
    }

    private static async Task<List<Complaint>> MarkedComplaints(
        IApplicationDbContext db,
        Guid[] userIds,
        Guid[] reportIds,
        Guid[] commentIds,
        CancellationToken cancellationToken)
    {
        var marked = await db.Complaints.Where(item => item.Details != null && item.Details.StartsWith(Marker)).ToListAsync(cancellationToken);
        var related = new List<Complaint>();
        if (reportIds.Length > 0)
        {
            related.AddRange(await db.Complaints.Where(item => reportIds.Contains(item.TargetId)).ToListAsync(cancellationToken));
        }

        if (commentIds.Length > 0)
        {
            related.AddRange(await db.Complaints.Where(item => commentIds.Contains(item.TargetId)).ToListAsync(cancellationToken));
        }

        if (userIds.Length > 0)
        {
            related.AddRange(await db.Complaints.Where(item => userIds.Contains(item.ReporterId)).ToListAsync(cancellationToken));
        }

        return marked.Concat(related).DistinctBy(item => item.Id).ToList();
    }

    private static async Task<List<TakedownRequest>> MarkedTakedowns(
        IApplicationDbContext db,
        Guid[] reportIds,
        Guid[] commentIds,
        CancellationToken cancellationToken)
    {
        var marked = await db.TakedownRequests.Where(item => item.Description.StartsWith(Marker)).ToListAsync(cancellationToken);
        var related = new List<TakedownRequest>();
        if (reportIds.Length > 0)
        {
            related.AddRange(await db.TakedownRequests.Where(item => reportIds.Contains(item.TargetId)).ToListAsync(cancellationToken));
        }

        if (commentIds.Length > 0)
        {
            related.AddRange(await db.TakedownRequests.Where(item => commentIds.Contains(item.TargetId)).ToListAsync(cancellationToken));
        }

        return marked.Concat(related).DistinctBy(item => item.Id).ToList();
    }

    private static GeoPoint NearBogota(int index) =>
        new(4.7110 + (index - 3.5) * 0.004, -74.0721 + ((index % 4) - 1.5) * 0.004);

    private static string Mark(string suffix) => Marker + " " + suffix;

    private readonly record struct SampleReport(string CategoryCode, bool Anonymous, ComplaintReason? Complaint, bool Comment);
}

public sealed record SampleUser(string Email, string UserName, string GivenNames, string Surnames, string Phone);

public sealed record DevSeedResult(int Users, int Reports, int Comments, int Complaints, int Takedowns);

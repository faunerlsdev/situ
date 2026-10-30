using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Legal;
using Situ.Domain.Live;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;

namespace Situ.Application.Development;

/// <summary>
/// Two adult accounts and sample content near Bogotá for App Review.
/// Passwords come from the caller. Nothing here is written to a log.
/// </summary>
public static class ReviewDemoSeed
{
    public const string ReviewEmailAddress = "revisor.demo@situ.test";
    public const string ReviewUserName = "revisor_demo";
    public const string DeleteEmailAddress = "revisor.borrado@situ.test";
    public const string DeleteUserName = "revisor_borrado";

    private const string Marker = "[review-demo]";

    private static readonly byte[] Photo = Convert.FromHexString(
        "FFD8FFE000104A46494600010100000100010000FFDB004300080606070605080707070909080A0C140D0C0B0B0C1912130F141D1A1F1E1D1A1C1C20242E2720222C231C1C2837292C30313434341F27393D38323C2E333432FFC0000B080001000101011100FFC4001F0000010501010101010100000000000000000102030405060708090A0BFFC400B5100002010303020403050504040000017D01020300041105122131410613516107227114328191A1082342B1C11552D1F02433627282090A161718191A25262728292A3435363738393A434445464748494A535455565758595A636465666768696A737475767778797A838485868788898A92939495969798999AA2A3A4A5A6A7A8A9AAB2B3B4B5B6B7B8B9BAC2C3C4C5C6C7C8C9CAD3D4D5D6D7D8D9DAE1E2E3E4E5E6E7E8E9EAF1F2F3F4F5F6F7F8F9FAFFDA000C03010002110311003F00F9FFD9");

    public static async Task<Result<ReviewDemoResult>> ApplyAsync(
        IApplicationDbContext db,
        IPasswordHasher passwords,
        IFileStorage files,
        IClock clock,
        string? reviewPassword,
        string? deletePassword,
        CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(reviewPassword) || !PasswordPolicy.IsValid(deletePassword))
        {
            return Result<ReviewDemoResult>.Failure(Error.Validation(
                "review_demo.password",
                "Cada contraseña necesita 8 caracteres, con mayúscula, minúscula y un dígito."));
        }

        var city = await db.Cities.FirstOrDefaultAsync(item => item.Name == "Bogotá", cancellationToken);
        if (city is null)
        {
            return Result<ReviewDemoResult>.Failure(Error.NotFound(
                "review_demo.city",
                "No está la ciudad Bogotá. Aplica las migraciones antes de review:seed-demo."));
        }

        var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.IsActive, cancellationToken);
        if (category is null)
        {
            return Result<ReviewDemoResult>.Failure(Error.NotFound(
                "review_demo.category",
                "No hay una categoría de reportes activa."));
        }

        var country = await db.Countries.FirstAsync(item => item.Id == city.CountryId, cancellationToken);
        var now = clock.UtcNow;
        var review = await EnsureUserAsync(
            db, passwords, country, city, now, ReviewEmailAddress, ReviewUserName, "Revisor", "Demo", "Revisor Demo", "+573001110021", reviewPassword!, cancellationToken);
        if (review.IsFailure)
        {
            return Result<ReviewDemoResult>.Failure(review.Error!);
        }

        var delete = await EnsureUserAsync(
            db, passwords, country, city, now, DeleteEmailAddress, DeleteUserName, "Revisor", "Borrado", "Revisor Borrado", "+573001110022", deletePassword!, cancellationToken);
        if (delete.IsFailure)
        {
            return Result<ReviewDemoResult>.Failure(delete.Error!);
        }

        await AcceptCurrentDocumentsAsync(db, review.Value!.Id, country.Id, now, cancellationToken);
        await AcceptCurrentDocumentsAsync(db, delete.Value!.Id, country.Id, now, cancellationToken);
        await EnsureContentAsync(db, files, category, city.Id, review.Value, delete.Value, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result<ReviewDemoResult>.Success(new ReviewDemoResult(ReviewEmailAddress, DeleteEmailAddress));
    }

    private static async Task<Result<User>> EnsureUserAsync(
        IApplicationDbContext db,
        IPasswordHasher passwords,
        Country country,
        City city,
        DateTimeOffset now,
        string emailAddress,
        string userName,
        string givenNames,
        string surnames,
        string displayName,
        string phone,
        string password,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(emailAddress);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (user is null)
        {
            var handle = UserName.Create(userName);
            if (await db.Users.AnyAsync(item => item.UserName == handle, cancellationToken))
            {
                return Result<User>.Failure(Error.Conflict(
                    "review_demo.username_taken",
                    $"El usuario {userName} ya pertenece a otra cuenta."));
            }

            user = User.Register(
                email,
                handle,
                PersonName.Create(givenNames),
                PersonName.Create(surnames),
                PhoneNumber.Create(phone),
                passwords.Hash(password),
                new DateOnly(1990, 1, 15),
                country.Id,
                city.RegionId,
                city.Id,
                "es-CO",
                country.MinimumAge,
                now);
            user.ConfirmEmail(now);
            db.Users.Add(user);
            db.UserProfiles.Add(UserProfile.CreateDefault(user.Id, displayName));
            return Result<User>.Success(user);
        }

        if (user.Status is UserStatus.Suspended or UserStatus.Banned)
        {
            user.Reinstate(now);
        }

        if (user.Status == UserStatus.DeletionRequested)
        {
            user.CancelDeletion(now);
        }

        user.ConfirmEmail(now);
        user.ResetPassword(passwords.Hash(password), now);
        if (!await db.UserProfiles.AnyAsync(item => item.UserId == user.Id, cancellationToken))
        {
            db.UserProfiles.Add(UserProfile.CreateDefault(user.Id, displayName));
        }

        return Result<User>.Success(user);
    }

    private static async Task AcceptCurrentDocumentsAsync(
        IApplicationDbContext db,
        Guid userId,
        Guid countryId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var documents = await db.LegalDocuments.Where(item => item.IsActive && item.CountryId == countryId).ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            var accepted = await db.ConsentRecords.AnyAsync(
                item => item.UserId == userId && item.DocumentType == document.Type && item.Version == document.Version && item.RevokedAt == null,
                cancellationToken);
            if (accepted)
            {
                continue;
            }

            db.ConsentRecords.Add(ConsentRecord.Accept(
                userId,
                document.Type,
                document.Version,
                countryId,
                ConsentMethod.Registration,
                "127.0.0.1",
                "review-demo",
                now));
        }
    }

    private static async Task EnsureContentAsync(
        IApplicationDbContext db,
        IFileStorage files,
        ReportCategory category,
        Guid cityId,
        User author,
        User commenter,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var samples = new (string Description, double Latitude, double Longitude)[]
        {
            ("Trancón en el centro. Varios carros detenidos.", 4.7110, -74.0721),
            ("Hueco en la vía principal. Conduce con cuidado.", 4.7150, -74.0680),
            ("Basura acumulada en una esquina del barrio.", 4.7070, -74.0760),
        };

        Report? first = null;
        for (var index = 0; index < samples.Length; index++)
        {
            var sample = samples[index];
            var description = Marker + " " + sample.Description;
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Description == description, cancellationToken);
            if (report is null)
            {
                report = Report.Create(
                    author.Id,
                    category.Id,
                    cityId,
                    new GeoPoint(sample.Latitude, sample.Longitude),
                    description,
                    false,
                    false,
                    category.EffectiveLifetime(),
                    now);
                db.Reports.Add(report);
                var key = $"review-demo/reports/{index + 1}.jpg";
                await files.SaveAsync(key, Photo, cancellationToken);
                var media = ReportMedia.Create(report.Id, author.Id, MediaType.Image, key, Photo.Length, null, 0, false, now);
                media.MarkReady(null);
                db.ReportMedias.Add(media);
            }

            first ??= report;
        }

        var commentText = Marker + " Sigue igual, acabo de pasar.";
        if (first is not null && !await db.Comments.AnyAsync(item => item.Text == commentText, cancellationToken))
        {
            db.Comments.Add(Comment.Create(
                ContentTargetType.Report,
                first.Id,
                commenter.Id,
                commentText,
                false,
                null,
                null,
                first.AuthorId,
                now));
            first.AddComment();
        }

        const string publicPath = "review-demo-centro";
        if (!await db.LiveStreams.AnyAsync(item => item.PublicPath == publicPath, cancellationToken))
        {
            var stream = LiveStream.Schedule(
                author.Id,
                36,
                true,
                category.Id,
                cityId,
                new GeoPoint(4.7110, -74.0721),
                "Tráfico en el centro",
                ContentVisibility.Public,
                publicPath,
                "review-demo-publish-hash",
                now.AddHours(2),
                now);
            if (!stream.AcceptPublishKey("review-demo-publish-hash", now) || !stream.MarkLive(now))
            {
                return;
            }

            const string recordingKey = "review-demo/live/recording.bin";
            await files.SaveAsync(recordingKey, "review-demo"u8.ToArray(), cancellationToken);
            stream.End(recordingKey, now);
            db.LiveStreams.Add(stream);
        }
    }
}

public sealed record ReviewDemoResult(string ReviewEmail, string DeleteEmail);

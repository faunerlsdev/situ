using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity;
using Situ.Application.Common.Results;
using Situ.Domain.Identity;
using Situ.Domain.Retention;

namespace Situ.Application.Features.Retention.RequestPersonalDataExport;

public sealed record RequestPersonalDataExportRequest(Guid UserId);

public sealed record RequestPersonalDataExportResponse(Guid ExportId, DateTimeOffset? ExpiresAt);

public static class PersonalDataSnapshot
{
    public static string Build(User user)
    {
        return JsonSerializer.Serialize(new
        {
            email = user.Email.Value,
            userName = user.UserName.Value,
            givenNames = user.GivenNames.Value,
            surnames = user.Surnames.Value,
            phone = user.Phone?.Value,
            dateOfBirth = user.DateOfBirth.ToString("yyyy-MM-dd"),
            countryId = user.CountryId,
            regionId = user.RegionId,
            cityId = user.CityId,
            locale = user.Locale,
        }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}

public sealed class RequestPersonalDataExportHandler(IApplicationDbContext db, IClock clock, IFileStorage files)
    : IHandler<RequestPersonalDataExportRequest, Result<RequestPersonalDataExportResponse>>
{
    public async Task<Result<RequestPersonalDataExportResponse>> Handle(
        RequestPersonalDataExportRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return Result<RequestPersonalDataExportResponse>.Failure(AuthErrors.Unauthenticated());
        }

        var now = clock.UtcNow;
        var export = DataExportRequest.Create(user.Id, now);
        export.StartProcessing(now);
        var key = $"exports/{user.Id:N}/{export.Id:N}.json";
        await files.SaveAsync(key, Encoding.UTF8.GetBytes(PersonalDataSnapshot.Build(user)), cancellationToken);
        export.Complete(key, now);
        db.DataExportRequests.Add(export);
        await db.SaveChangesAsync(cancellationToken);
        return Result<RequestPersonalDataExportResponse>.Success(new RequestPersonalDataExportResponse(export.Id, export.ExpiresAt));
    }
}

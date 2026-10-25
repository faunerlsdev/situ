using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.UploadReportMedia;

public sealed record UploadReportMediaRequest(Guid UserId, Stream Content, long Length);

public sealed record UploadReportMediaResponse(Guid MediaId, string Type, string Url, string? ThumbnailUrl);

public sealed class UploadReportMediaHandler(
    IApplicationDbContext db,
    IClock clock,
    IReportMediaProcessor processor,
    IFileStorage storage,
    IMediaUrlSigner urls)
    : IHandler<UploadReportMediaRequest, Result<UploadReportMediaResponse>>
{
    public async Task<Result<UploadReportMediaResponse>> Handle(UploadReportMediaRequest request, CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var limits = new MediaProcessingLimits(
            ReportSettings.Long(settings, SystemSettingKeys.ReportMaxImageBytes, 10 * 1024 * 1024),
            ReportSettings.Long(settings, SystemSettingKeys.ReportMaxVideoBytes, 100 * 1024 * 1024),
            ReportSettings.Int(settings, SystemSettingKeys.ReportMaxVideoSeconds, 60));
        var processed = await processor.ProcessAsync(request.Content, request.Length, limits, cancellationToken);
        if (processed.IsFailure)
        {
            return Result<UploadReportMediaResponse>.Failure(processed.Error!);
        }

        var file = processed.Value!;
        var now = clock.UtcNow;
        var key = $"reports/{Guid.CreateVersion7():N}";
        await storage.SaveAsync(key, file.Body, cancellationToken);
        string? thumbnailKey = null;
        if (file.Thumbnail is { Length: > 0 } thumbnail)
        {
            thumbnailKey = key + "-thumb";
            await storage.SaveAsync(thumbnailKey, thumbnail, cancellationToken);
        }

        var media = ReportMedia.Stage(request.UserId, file.Type, key, file.Body.Length, file.DurationSeconds, facesBlurred: false, now);
        media.MarkReady(thumbnailKey);
        db.ReportMedias.Add(media);
        await db.SaveChangesAsync(cancellationToken);
        var signed = urls.Sign(media.Id, now.AddMinutes(15));
        var thumbSigned = urls.Sign(media.Id, now.AddMinutes(15));
        return Result<UploadReportMediaResponse>.Success(new UploadReportMediaResponse(
            media.Id,
            file.Type.ToString(),
            ReportPresentation.ReadUrl(media.Id, signed.ExpiresUnix, signed.Signature),
            thumbnailKey is null ? null : ReportPresentation.ReadUrl(media.Id, thumbSigned.ExpiresUnix, thumbSigned.Signature) + "&kind=thumb"));
    }
}

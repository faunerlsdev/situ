using Situ.Application.Common.Results;
using Situ.Domain.Reports;

namespace Situ.Application.Common.Abstractions;

public sealed record MediaProcessingLimits(long MaxImageBytes, long MaxVideoBytes, int MaxVideoSeconds);

public sealed record ProcessedReportFile(
    MediaType Type,
    string ContentType,
    byte[] Body,
    byte[]? Thumbnail,
    int? DurationSeconds);

/// <summary>Validates the real file type, strips image metadata and builds a thumbnail.</summary>
public interface IReportMediaProcessor
{
    Task<Result<ProcessedReportFile>> ProcessAsync(
        Stream content,
        long length,
        MediaProcessingLimits limits,
        CancellationToken cancellationToken);
}

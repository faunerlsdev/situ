using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Reports;
using SkiaSharp;

namespace Situ.Infrastructure.Reports;

/// <summary>
/// Images are decoded and re-encoded, which drops EXIF (including GPS).
/// Videos are stored as uploaded after a container check.
/// TODO: transcode videos to a single streaming format and generate a poster frame.
/// </summary>
public sealed class SkiaReportMediaProcessor : IReportMediaProcessor
{
    private const int MaxEdge = 2048;
    private const int ThumbEdge = 480;

    public async Task<Result<ProcessedReportFile>> ProcessAsync(
        Stream content,
        long length,
        MediaProcessingLimits limits,
        CancellationToken cancellationToken)
    {
        if (length <= 0)
        {
            return Result<ProcessedReportFile>.Failure(Error.Validation("report.media_empty", "El archivo está vacío."));
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            return Result<ProcessedReportFile>.Failure(Error.Validation("report.media_empty", "El archivo está vacío."));
        }

        if (IsImage(bytes))
        {
            if (bytes.Length > limits.MaxImageBytes)
            {
                return Result<ProcessedReportFile>.Failure(Error.Validation("report.image_too_large", "La imagen supera el tamaño permitido."));
            }

            return EncodeImage(bytes);
        }

        if (IsMp4(bytes))
        {
            if (bytes.Length > limits.MaxVideoBytes)
            {
                return Result<ProcessedReportFile>.Failure(Error.Validation("report.video_too_large", "El video supera el tamaño permitido."));
            }

            if (!TryReadMp4Duration(bytes, out var seconds))
            {
                return Result<ProcessedReportFile>.Failure(Error.Validation("report.video_unreadable", "No pudimos leer la duración del video."));
            }

            if (seconds > limits.MaxVideoSeconds)
            {
                return Result<ProcessedReportFile>.Failure(Error.Validation("report.video_too_long", "El video supera la duración permitida."));
            }

            return Result<ProcessedReportFile>.Success(new ProcessedReportFile(MediaType.Video, "video/mp4", bytes, null, seconds));
        }

        return Result<ProcessedReportFile>.Failure(Error.Validation("report.media_type", "Sube una imagen JPG, PNG o WebP, o un video MP4."));
    }

    private static Result<ProcessedReportFile> EncodeImage(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
        {
            return Result<ProcessedReportFile>.Failure(Error.Validation("report.media_type", "No pudimos leer la imagen."));
        }

        using var fitted = Fit(bitmap, MaxEdge);
        using var thumb = Fit(bitmap, ThumbEdge);
        return Result<ProcessedReportFile>.Success(new ProcessedReportFile(
            MediaType.Image,
            "image/jpeg",
            Encode(fitted),
            Encode(thumb),
            null));
    }

    private static SKBitmap Fit(SKBitmap source, int edge)
    {
        var scale = Math.Min(1d, Math.Min((double)edge / source.Width, (double)edge / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var info = new SKImageInfo(width, height);
        var bitmap = new SKBitmap(info);
        source.ScalePixels(bitmap, SKFilterQuality.Medium);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }

    private static bool IsImage(byte[] bytes) =>
        bytes.Length >= 8 && (
            (bytes[0] == 0xFF && bytes[1] == 0xD8)
            || (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            || (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'));

    private static bool IsMp4(byte[] bytes) =>
        bytes.Length > 12 && bytes[4] == (byte)'f' && bytes[5] == (byte)'t' && bytes[6] == (byte)'y' && bytes[7] == (byte)'p';

    internal static bool TryReadMp4Duration(byte[] bytes, out int seconds)
    {
        seconds = 0;
        for (var index = 0; index < bytes.Length - 32; index++)
        {
            if (bytes[index] != (byte)'m' || bytes[index + 1] != (byte)'v' || bytes[index + 2] != (byte)'h' || bytes[index + 3] != (byte)'d')
            {
                continue;
            }

            var version = bytes[index + 4];
            if (version == 0 && index + 24 < bytes.Length)
            {
                var timescale = ReadUInt32(bytes, index + 16);
                var duration = ReadUInt32(bytes, index + 20);
                if (timescale == 0)
                {
                    return false;
                }

                seconds = (int)Math.Ceiling(duration / (double)timescale);
                return true;
            }

            if (version == 1 && index + 36 < bytes.Length)
            {
                var timescale = ReadUInt32(bytes, index + 24);
                var duration = ReadUInt64(bytes, index + 28);
                if (timescale == 0)
                {
                    return false;
                }

                seconds = (int)Math.Ceiling(duration / (double)timescale);
                return true;
            }
        }

        return false;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    private static ulong ReadUInt64(byte[] bytes, int offset) =>
        ((ulong)ReadUInt32(bytes, offset) << 32) | ReadUInt32(bytes, offset + 4);
}

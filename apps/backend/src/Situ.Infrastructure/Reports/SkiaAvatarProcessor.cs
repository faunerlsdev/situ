using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using SkiaSharp;

namespace Situ.Infrastructure.Reports;

/// <summary>
/// Center-crops to a square and re-encodes as JPEG. Decoding drops EXIF, including GPS.
/// </summary>
public sealed class SkiaAvatarProcessor : IAvatarImageProcessor
{
    public const int Edge = 512;

    public Result<byte[]> Process(byte[] content, int maxBytes)
    {
        if (content.Length == 0)
        {
            return Result<byte[]>.Failure(Error.Validation("avatar.empty", "La foto está vacía."));
        }

        if (content.Length > maxBytes)
        {
            return Result<byte[]>.Failure(Error.Validation("avatar.too_large", "La foto supera el tamaño permitido."));
        }

        if (!IsImage(content))
        {
            return Result<byte[]>.Failure(Error.Validation("avatar.invalid_image", "Sube una imagen JPG, PNG o WebP."));
        }

        using var bitmap = SKBitmap.Decode(content);
        if (bitmap is null || bitmap.Width < 1 || bitmap.Height < 1)
        {
            return Result<byte[]>.Failure(Error.Validation("avatar.invalid_image", "No pudimos leer la foto."));
        }

        using var square = CenterSquare(bitmap);
        var info = new SKImageInfo(Edge, Edge, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var scaled = new SKBitmap(info);
        square.ScalePixels(scaled, SKFilterQuality.Medium);
        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return Result<byte[]>.Success(data.ToArray());
    }

    private static SKBitmap CenterSquare(SKBitmap source)
    {
        var side = Math.Min(source.Width, source.Height);
        var left = (source.Width - side) / 2;
        var top = (source.Height - side) / 2;
        var subset = new SKBitmap(side, side);
        using var canvas = new SKCanvas(subset);
        canvas.DrawBitmap(
            source,
            new SKRect(left, top, left + side, top + side),
            new SKRect(0, 0, side, side));
        return subset;
    }

    private static bool IsImage(byte[] bytes) =>
        bytes.Length >= 8 && (
            (bytes[0] == 0xFF && bytes[1] == 0xD8)
            || (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            || (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'));
}

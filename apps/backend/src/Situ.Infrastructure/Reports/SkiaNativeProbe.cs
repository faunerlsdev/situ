using SkiaSharp;

namespace Situ.Infrastructure.Reports;

/// <summary>Loads libSkiaSharp. A missing or unloadable library throws before a photo is accepted.</summary>
public static class SkiaNativeProbe
{
    public static void EnsureLoaded()
    {
        using var bitmap = new SKBitmap(1, 1, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 50);
        if (encoded is null || encoded.Size == 0)
        {
            throw new InvalidOperationException("Skia did not encode an image.");
        }
    }
}

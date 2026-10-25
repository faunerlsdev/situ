using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using SkiaSharp;

namespace Situ.Infrastructure.Reports;

public sealed class SkiaAdminIconProcessor : IAdminIconProcessor
{
    public Result<byte[]> Process(byte[] content, int maxBytes, int minEdge, int maxEdge)
    {
        if (content.Length == 0)
        {
            return Result<byte[]>.Failure(Error.Validation("icon.empty", "El archivo está vacío."));
        }

        if (content.Length > maxBytes)
        {
            return Result<byte[]>.Failure(Error.Validation("icon.too_large", "La imagen supera el tamaño permitido."));
        }

        if (!IsImage(content))
        {
            return Result<byte[]>.Failure(Error.Validation("icon.invalid_image", "Sube una imagen JPG, PNG o WebP."));
        }

        using var bitmap = SKBitmap.Decode(content);
        if (bitmap is null || bitmap.Width < minEdge || bitmap.Height < minEdge || bitmap.Width > maxEdge || bitmap.Height > maxEdge)
        {
            return Result<byte[]>.Failure(Error.Validation("icon.invalid_dimensions", "La imagen no tiene el tamaño permitido."));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Result<byte[]>.Success(data.ToArray());
    }

    private static bool IsImage(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return true;
        }

        if (content.Length >= 8 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
        {
            return true;
        }

        return content.Length >= 12 && content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46
            && content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50;
    }
}

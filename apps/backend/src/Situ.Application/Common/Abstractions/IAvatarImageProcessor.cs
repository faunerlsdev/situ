using Situ.Application.Common.Results;

namespace Situ.Application.Common.Abstractions;

/// <summary>Turns an uploaded profile photo into a square JPEG without the original metadata.</summary>
public interface IAvatarImageProcessor
{
    Result<byte[]> Process(byte[] content, int maxBytes);
}

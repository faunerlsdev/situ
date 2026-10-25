using Situ.Application.Common.Results;

namespace Situ.Application.Common.Abstractions;

/// <summary>Validates a real image, checks its edges and re-encodes it without metadata.</summary>
public interface IAdminIconProcessor
{
    Result<byte[]> Process(byte[] content, int maxBytes, int minEdge, int maxEdge);
}

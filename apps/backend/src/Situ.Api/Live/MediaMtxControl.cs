using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Situ.Application.Features.Live;

namespace Situ.Api.Live;

public sealed class MediaMtxControl(
    HttpClient http,
    IOptions<LiveStreamOptions> options,
    ILogger<MediaMtxControl> logger) : IMediaMtxControl
{
    public async Task<IReadOnlyList<MediaMtxPath>> ListPathsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var page = await http.GetFromJsonAsync<PathPage>(Control("/v3/paths/list"), cancellationToken);
            return page?.Items?.Select(item => new MediaMtxPath(item.Name ?? string.Empty, item.Ready)).Where(item => item.Name.Length > 0).ToArray()
                ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning("MediaMTX path list failed ({ExceptionType}).", exception.GetType().Name);
            return [];
        }
    }

    public async Task<bool> KickPathAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var page = await http.GetFromJsonAsync<ConnPage>(Control("/v3/rtmps/conns/list"), cancellationToken);
            var kicked = false;
            foreach (var connection in page?.Items ?? [])
            {
                if (!string.Equals(connection.Path, path, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(connection.Id))
                {
                    continue;
                }

                using var response = await http.PostAsync(Control($"/v3/rtmps/conns/kick/{Uri.EscapeDataString(connection.Id)}"), null, cancellationToken);
                kicked |= response.IsSuccessStatusCode;
            }

            return kicked;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning("MediaMTX kick failed ({ExceptionType}).", exception.GetType().Name);
            return false;
        }
    }

    private string Control(string path)
    {
        var root = options.Value.ControlUrl.TrimEnd('/');
        return root + path;
    }

    private sealed class PathPage
    {
        [JsonPropertyName("items")]
        public List<PathItem>? Items { get; set; }
    }

    private sealed class PathItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("ready")]
        public bool Ready { get; set; }
    }

    private sealed class ConnPage
    {
        [JsonPropertyName("items")]
        public List<ConnItem>? Items { get; set; }
    }

    private sealed class ConnItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }
}

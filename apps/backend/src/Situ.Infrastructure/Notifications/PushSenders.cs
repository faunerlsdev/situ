using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Situ.Application.Common.Abstractions;

namespace Situ.Infrastructure.Notifications;

public enum ExpoPushDelivery
{
    Authorized,
    Anonymous,
    Disabled,
}

public static class ExpoPushPolicy
{
    public static bool RequireAccessToken(string? environmentName, bool? configured)
    {
        if (configured is { } value)
        {
            return value;
        }

        return !string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    public static ExpoPushDelivery Decide(bool requireAccessToken, string? accessToken)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return ExpoPushDelivery.Authorized;
        }

        return requireAccessToken ? ExpoPushDelivery.Disabled : ExpoPushDelivery.Anonymous;
    }
}

public static class ExpoPushStartup
{
    public static void WarnIfDisabled(ILogger logger, ExpoPushDelivery delivery)
    {
        if (delivery != ExpoPushDelivery.Disabled)
        {
            return;
        }

        logger.LogWarning("Push is disabled because Notifications:ExpoAccessToken is missing and Notifications:RequireExpoAccessToken is true. The API keeps running.");
    }
}

public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushNote> notes, CancellationToken cancellationToken)
    {
        foreach (var note in notes)
        {
            logger.LogInformation("Push queued for device {DeviceId}.", note.DeviceId);
        }

        IReadOnlyList<PushResult> results = notes.Select(note => new PushResult(note.DeviceId, true, false, null)).ToList();
        return Task.FromResult(results);
    }
}

public sealed class DisabledPushSender : IPushSender
{
    public Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushNote> notes, CancellationToken cancellationToken)
    {
        IReadOnlyList<PushResult> results = notes.Select(note => new PushResult(note.DeviceId, false, false, "push_unconfigured")).ToList();
        return Task.FromResult(results);
    }
}

public sealed class ExpoPushSender(HttpClient http, ILogger logger, string? accessToken) : IPushSender
{
    public const int MaxBatch = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushNote> notes, CancellationToken cancellationToken)
    {
        var results = new List<PushResult>(notes.Count);
        for (var offset = 0; offset < notes.Count; offset += MaxBatch)
        {
            var batch = notes.Skip(offset).Take(MaxBatch).ToList();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://exp.host/--/api/v2/push/send")
            {
                Content = new StringContent(JsonSerializer.Serialize(batch.Select(note => new ExpoMessage(
                    note.Token,
                    note.Title,
                    note.Body,
                    note.Payload,
                    PushChannels.AndroidAlerts,
                    "high",
                    "default",
                    note.Badge)), JsonOptions), Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            }

            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException)
            {
                results.AddRange(batch.Select(note => new PushResult(note.DeviceId, false, false, "push_failed")));
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo push batch was rejected with status {StatusCode}.", (int)response.StatusCode);
                results.AddRange(batch.Select(note => new PushResult(note.DeviceId, false, false, "push_failed")));
                continue;
            }

            var body = await response.Content.ReadFromJsonAsync<ExpoResponse>(cancellationToken);
            var tickets = body?.Data ?? [];
            for (var index = 0; index < batch.Count; index++)
            {
                var ticket = index < tickets.Count ? tickets[index] : null;
                var notRegistered = string.Equals(ticket?.Details?.Error, "DeviceNotRegistered", StringComparison.Ordinal);
                var accepted = string.Equals(ticket?.Status, "ok", StringComparison.OrdinalIgnoreCase);
                results.Add(new PushResult(batch[index].DeviceId, accepted, notRegistered, accepted ? null : ticket?.Details?.Error ?? "push_failed"));
            }
        }

        return results;
    }

    private sealed record ExpoMessage(
        string To,
        string Title,
        string Body,
        PushPayload Data,
        string ChannelId,
        string Priority,
        string Sound,
        int Badge);

    private sealed class ExpoResponse
    {
        [JsonPropertyName("data")]
        public List<ExpoTicket> Data { get; set; } = [];
    }

    private sealed class ExpoTicket
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("details")]
        public ExpoDetails? Details { get; set; }
    }

    private sealed class ExpoDetails
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}

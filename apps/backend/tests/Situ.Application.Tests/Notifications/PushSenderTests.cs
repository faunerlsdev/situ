using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Situ.Application.Common.Abstractions;
using Situ.Infrastructure.Notifications;

namespace Situ.Application.Tests.Notifications;

public class PushSenderTests
{
    [Theory]
    [InlineData("Development", null, null, ExpoPushDelivery.Anonymous)]
    [InlineData("Development", false, null, ExpoPushDelivery.Anonymous)]
    [InlineData("Development", true, null, ExpoPushDelivery.Disabled)]
    [InlineData("Development", false, "secret", ExpoPushDelivery.Authorized)]
    [InlineData("Production", null, null, ExpoPushDelivery.Disabled)]
    [InlineData("Production", true, null, ExpoPushDelivery.Disabled)]
    [InlineData("Production", false, null, ExpoPushDelivery.Anonymous)]
    [InlineData("Production", true, "secret", ExpoPushDelivery.Authorized)]
    [InlineData("Staging", null, null, ExpoPushDelivery.Disabled)]
    [InlineData("Staging", null, "secret", ExpoPushDelivery.Authorized)]
    public void Expo_access_token_is_required_outside_development_unless_configured(string environment, bool? required, string? token, ExpoPushDelivery expected)
    {
        var require = ExpoPushPolicy.RequireAccessToken(environment, required);
        if (required is { } configured)
        {
            Assert.Equal(configured, require);
        }

        Assert.Equal(expected, ExpoPushPolicy.Decide(require, token));
    }

    [Fact]
    public void A_missing_required_token_warns_once_and_does_not_throw()
    {
        var logger = new ListLogger();
        ExpoPushStartup.WarnIfDisabled(logger, ExpoPushDelivery.Disabled);
        ExpoPushStartup.WarnIfDisabled(logger, ExpoPushDelivery.Authorized);
        Assert.Equal(1, logger.Warnings);
        Assert.DoesNotContain("secret", logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expo_messages_use_the_app_channel_a_high_priority_and_the_badge()
    {
        var capture = new CaptureHandler();
        var sender = new ExpoPushSender(new HttpClient(capture), new ListLogger(), "expo-token");
        var reportId = Guid.NewGuid();
        var note = new PushNote(
            Guid.NewGuid(),
            "ExponentPushToken[abc]",
            "En tu ciudad",
            "Nuevo reporte en Bogotá: Hurto",
            PushPayload.For(Guid.NewGuid(), "NearbyReport", "Report", reportId, reportId),
            4);

        var results = await sender.SendAsync([note], CancellationToken.None);

        Assert.True(Assert.Single(results).Accepted);
        Assert.Equal("Bearer expo-token", capture.Authorization);
        using var json = JsonDocument.Parse(capture.Body);
        var message = json.RootElement[0];
        Assert.Equal(PushChannels.AndroidAlerts, message.GetProperty("channelId").GetString());
        Assert.Equal("high", message.GetProperty("priority").GetString());
        Assert.Equal("default", message.GetProperty("sound").GetString());
        Assert.Equal(4, message.GetProperty("badge").GetInt32());
        Assert.Equal(reportId.ToString(), message.GetProperty("data").GetProperty("reportId").GetString());
        Assert.False(message.GetProperty("data").TryGetProperty("latitude", out _));
    }

    [Fact]
    public async Task A_summary_omits_the_report_and_development_can_send_without_a_token()
    {
        var capture = new CaptureHandler();
        var sender = new ExpoPushSender(new HttpClient(capture), new ListLogger(), null);
        var note = new PushNote(
            Guid.NewGuid(),
            "ExponentPushToken[abc]",
            "En tu zona",
            "Hay 2 reportes nuevos en tu zona",
            PushPayload.Summary(Guid.NewGuid()),
            2);

        await sender.SendAsync([note], CancellationToken.None);

        Assert.Null(capture.Authorization);
        using var json = JsonDocument.Parse(capture.Body);
        var data = json.RootElement[0].GetProperty("data");
        Assert.Equal("Summary", data.GetProperty("type").GetString());
        Assert.False(data.TryGetProperty("reportId", out _));
        Assert.False(data.TryGetProperty("targetId", out _));
        Assert.Equal(PushChannels.AndroidAlerts, json.RootElement[0].GetProperty("channelId").GetString());
    }

    [Fact]
    public async Task A_disabled_sender_does_not_call_expo()
    {
        var sender = new DisabledPushSender();
        var result = Assert.Single(await sender.SendAsync([
            new PushNote(Guid.NewGuid(), "ExponentPushToken[abc]", "t", "b", PushPayload.Summary(Guid.NewGuid()), 1),
        ], CancellationToken.None));
        Assert.False(result.Accepted);
        Assert.False(result.DeviceNotRegistered);
        Assert.Equal("push_unconfigured", result.ErrorCode);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"status":"ok"}]}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ListLogger : ILogger
    {
        public int Warnings { get; private set; }
        public string Text { get; private set; } = "";

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings++;
            }

            Text += formatter(state, exception);
        }
    }
}

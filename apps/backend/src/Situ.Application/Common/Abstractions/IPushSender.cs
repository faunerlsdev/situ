namespace Situ.Application.Common.Abstractions;

/// <summary>Must match the Android channel created by the mobile app.</summary>
public static class PushChannels
{
    public const string AndroidAlerts = "zone-alerts";
}

/// <summary>Opaque ids only. No names, text, distances or coordinates.</summary>
public sealed record PushPayload(string NotificationId, string Type, string? TargetType, string? TargetId, string? ReportId)
{
    public static PushPayload For(Guid notificationId, string type, string? targetType, Guid? targetId, Guid? reportId) =>
        new(notificationId.ToString(), type, targetType, targetId?.ToString(), reportId?.ToString());

    public static PushPayload Summary(Guid notificationId) =>
        new(notificationId.ToString(), "Summary", null, null, null);
}

public sealed record PushNote(Guid DeviceId, string Token, string Title, string Body, PushPayload Payload, int Badge);

public sealed record PushResult(Guid DeviceId, bool Accepted, bool DeviceNotRegistered, string? ErrorCode);

public interface IPushSender
{
    Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushNote> notes, CancellationToken cancellationToken);
}

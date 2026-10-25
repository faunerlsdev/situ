using Situ.Api.Auth;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Notifications.Devices;
using Situ.Application.Features.Notifications.Inbox;
using Situ.Application.Features.Notifications.Settings;
using Situ.Api.Http;

namespace Situ.Api.Endpoints.Notifications;

public sealed class NotificationEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me/notification-settings", GetSettings)
            .WithName("GetNotificationSettings").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<NotificationSettingsResponse>();
        group.MapPut("/me/notification-settings", UpdateSettings)
            .WithName("UpdateNotificationSettings").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<NotificationSettingsResponse>();
        group.MapGet("/me/interest-cities", ListCities)
            .WithName("ListInterestCities").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<InterestCitiesResponse>();
        group.MapPut("/me/interest-cities/{cityId:guid}", AddCity)
            .WithName("AddInterestCity").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<InterestCitiesResponse>();
        group.MapDelete("/me/interest-cities/{cityId:guid}", RemoveCity)
            .WithName("RemoveInterestCity").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<InterestCitiesResponse>();
        group.MapGet("/me/category-interests", GetCategories)
            .WithName("GetCategoryInterests").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<CategoryInterestsResponse>();
        group.MapPut("/me/category-interests", UpdateCategories)
            .WithName("UpdateCategoryInterests").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<CategoryInterestsResponse>();
        group.MapPut("/devices/current/push-token", SetToken)
            .WithName("SetPushToken").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("notification-device");
        group.MapDelete("/devices/current/push-token", ClearToken)
            .WithName("ClearPushToken").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("notification-device");
        group.MapPut("/devices/current/alert-location", SetLocation)
            .WithName("SetAlertLocation").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("notification-device");
        group.MapDelete("/devices/current/alert-location", ClearLocation)
            .WithName("ClearAlertLocation").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .RequireRateLimiting("notification-device");
        group.MapGet("/notifications", List)
            .WithName("ListNotifications").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<ListNotificationsResponse>();
        group.MapGet("/notifications/unread-count", Unread)
            .WithName("UnreadNotificationCount").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User)
            .Produces<UnreadCountResponse>();
        group.MapPost("/notifications/{id:guid}/read", Read)
            .WithName("MarkNotificationRead").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User);
        group.MapPost("/notifications/read-all", ReadAll)
            .WithName("MarkAllNotificationsRead").WithTags("Notifications").RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> GetSettings(ICurrentUser currentUser, IHandler<GetNotificationSettingsRequest, Result<NotificationSettingsResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new GetNotificationSettingsRequest(userId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateSettings(SettingsBody body, ICurrentUser currentUser, IHandler<UpdateNotificationSettingsRequest, Result<NotificationSettingsResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new UpdateNotificationSettingsRequest(userId, body.PushEnabled, body.NearbyRadiusMeters, body.NotifyNearbyReports, body.NotifyFriendReports, body.NotifyFriendLives, body.NotifyImportantNews, body.QuietHoursStart, body.QuietHoursEnd, body.TimeZoneId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> ListCities(ICurrentUser currentUser, IHandler<ListInterestCitiesRequest, Result<InterestCitiesResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new ListInterestCitiesRequest(userId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> AddCity(Guid cityId, ICurrentUser currentUser, IHandler<AddInterestCityRequest, Result<InterestCitiesResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new AddInterestCityRequest(userId, cityId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> RemoveCity(Guid cityId, ICurrentUser currentUser, IHandler<RemoveInterestCityRequest, Result<InterestCitiesResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new RemoveInterestCityRequest(userId, cityId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetCategories(ICurrentUser currentUser, IHandler<GetCategoryInterestsRequest, Result<CategoryInterestsResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new GetCategoryInterestsRequest(userId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateCategories(CategoriesBody body, ICurrentUser currentUser, IHandler<UpdateCategoryInterestsRequest, Result<CategoryInterestsResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new UpdateCategoryInterestsRequest(userId, body.MutedCategoryIds), cancellationToken)).ToHttpResult();

    private static async Task<IResult> SetToken(TokenBody body, ICurrentUser currentUser, IHandler<SetPushTokenRequest, Result> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new SetPushTokenRequest(userId, currentUser.DeviceId, body.Token), cancellationToken)).ToHttpResult();

    private static async Task<IResult> ClearToken(ICurrentUser currentUser, IHandler<ClearPushTokenRequest, Result> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new ClearPushTokenRequest(userId, currentUser.DeviceId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> SetLocation(LocationBody body, ICurrentUser currentUser, IHandler<SetAlertLocationRequest, Result> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new SetAlertLocationRequest(userId, currentUser.DeviceId, body.Latitude, body.Longitude, body.AccuracyMeters), cancellationToken)).ToHttpResult();

    private static async Task<IResult> ClearLocation(ICurrentUser currentUser, IHandler<ClearAlertLocationRequest, Result> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new ClearAlertLocationRequest(userId, currentUser.DeviceId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> List(string? cursor, int? limit, ICurrentUser currentUser, IHandler<ListNotificationsRequest, Result<ListNotificationsResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new ListNotificationsRequest(userId, cursor, limit is > 0 and <= 50 ? limit.Value : 20), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Unread(ICurrentUser currentUser, IHandler<UnreadCountRequest, Result<UnreadCountResponse>> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new UnreadCountRequest(userId), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Read(Guid id, ICurrentUser currentUser, IHandler<MarkNotificationReadRequest, Result> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new MarkNotificationReadRequest(userId, id), cancellationToken)).ToHttpResult();

    private static async Task<IResult> ReadAll(ICurrentUser currentUser, IHandler<MarkAllNotificationsReadRequest, Result> handler, CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Results.Unauthorized()
            : (await handler.Handle(new MarkAllNotificationsReadRequest(userId), cancellationToken)).ToHttpResult();

    private sealed record SettingsBody(bool PushEnabled, double NearbyRadiusMeters, bool NotifyNearbyReports, bool NotifyFriendReports, bool NotifyFriendLives, bool NotifyImportantNews, string? QuietHoursStart, string? QuietHoursEnd, string TimeZoneId);
    private sealed record CategoriesBody(IReadOnlyList<Guid> MutedCategoryIds);
    private sealed record TokenBody(string Token);
    private sealed record LocationBody(double Latitude, double Longitude, double? AccuracyMeters);
}

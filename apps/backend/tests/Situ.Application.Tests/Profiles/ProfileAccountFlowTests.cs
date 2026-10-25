using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Application.Features.Identity.ChangePassword;
using Situ.Application.Features.Identity.GetSessionProfile;
using Situ.Application.Features.Identity.ListMyDevices;
using Situ.Application.Features.Identity.RevokeDevice;
using Situ.Application.Features.Profiles.Avatars;
using Situ.Application.Features.Profiles.CheckUserName;
using Situ.Application.Features.Profiles.UpdateMyProfile;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Domain.Profiles;
using Situ.Infrastructure.Reports;
using SkiaSharp;

namespace Situ.Application.Tests.Profiles;

public class ProfileAccountFlowTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task User_name_changes_are_unique_reserved_and_limited_to_thirty_days()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var ana = await UserAsync(db, clock, catalog, 30, "ana@situ.test", "ana");
        await UserAsync(db, clock, catalog, 30, "luis@situ.test", "luis");
        db.UserInterestCities.Add(UserInterestCity.Create(ana.Id, catalog.OtherCityId));
        await db.SaveChangesAsync();

        var taken = await Update(db, clock, ana.Id, new UpdateMyProfileRequest(ana.Id, "LUIS", "Ana", "Hola", null));
        var reserved = await Update(db, clock, ana.Id, new UpdateMyProfileRequest(ana.Id, "Situ", null, null, null));
        var blocked = await Update(db, clock, ana.Id, new UpdateMyProfileRequest(ana.Id, null, "Texto nazi", null, null));
        var renamed = await Update(db, clock, ana.Id, new UpdateMyProfileRequest(ana.Id, "ana.perez", "Ana Pérez", "Bio corta", catalog.OtherCityId));
        var tooSoon = await Update(db, clock, ana.Id, new UpdateMyProfileRequest(ana.Id, "otra", null, null, null));
        var tooLong = await Update(db, clock, ana.Id, new UpdateMyProfileRequest(ana.Id, null, null, new string('a', 161), null));

        Assert.Equal("user.username_taken", taken.Error!.Code);
        Assert.Equal("user.username_taken", reserved.Error!.Code);
        Assert.Equal("profile.content_not_allowed", blocked.Error!.Code);
        Assert.True(renamed.IsSuccess);
        Assert.Equal("profile.username_change_limited", tooSoon.Error!.Code);
        Assert.Equal(clock.UtcNow.AddDays(30), DateTimeOffset.Parse(tooSoon.Error.Extensions["retryAfter"]!.ToString()!));
        Assert.Equal("bio.too_long", tooLong.Error!.Code);

        var stored = await db.Users.SingleAsync(item => item.Id == ana.Id);
        Assert.Equal("ana.perez", stored.UserName.Value);
        Assert.Equal(catalog.OtherCityId, stored.CityId);
        Assert.Equal(catalog.OtherCityId, (await db.UserInterestCities.SingleAsync()).CityId);
        Assert.Equal("Bio corta", (await db.UserProfiles.SingleAsync(item => item.UserId == ana.Id)).Bio);

        clock.UtcNow = clock.UtcNow.AddDays(30);
        var later = await Update(db, clock, ana.Id, new UpdateMyProfileRequest(ana.Id, "ana.nueva", null, null, null));
        Assert.True(later.IsSuccess);
    }

    [Fact]
    public async Task Username_lookup_does_not_distinguish_reserved_from_taken()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var ana = await UserAsync(db, clock, catalog, 30, "ana@situ.test", "ana");
        await UserAsync(db, clock, catalog, 30, "luis@situ.test", "luis");

        var taken = await new CheckUserNameHandler(db).Handle(new CheckUserNameRequest(ana.Id, "LUIS"), CancellationToken.None);
        var reserved = await new CheckUserNameHandler(db).Handle(new CheckUserNameRequest(ana.Id, "situ"), CancellationToken.None);
        var mine = await new CheckUserNameHandler(db).Handle(new CheckUserNameRequest(ana.Id, "ana"), CancellationToken.None);
        var free = await new CheckUserNameHandler(db).Handle(new CheckUserNameRequest(ana.Id, "libre"), CancellationToken.None);

        Assert.Equal("{\"available\":false}", JsonSerializer.Serialize(taken.Value, Json));
        Assert.Equal("{\"available\":false}", JsonSerializer.Serialize(reserved.Value, Json));
        Assert.DoesNotContain("reserved", JsonSerializer.Serialize(reserved.Value, Json), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("luis", JsonSerializer.Serialize(taken.Value, Json), StringComparison.OrdinalIgnoreCase);
        Assert.True(mine.Value!.Available);
        Assert.True(free.Value!.Available);
    }

    [Fact]
    public async Task Session_profile_masks_the_email_and_omits_the_birth_date()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var ana = await UserAsync(db, clock, catalog, 30, "ana@correo.com", "ana");
        ana.ChangeUserName(UserName.Create("ana.p"), clock.UtcNow);
        await db.SaveChangesAsync();

        var profile = await new GetSessionProfileHandler(db, clock, new Signer(), new FakeLinks())
            .Handle(new GetSessionProfileRequest(ana.Id), CancellationToken.None);
        var json = JsonSerializer.Serialize(profile.Value, Json);

        Assert.Equal("a***@correo.com", profile.Value!.MaskedEmail);
        Assert.Equal(2026, profile.Value.MemberSinceYear);
        Assert.Equal(10, profile.Value.MemberSinceMonth);
        Assert.Equal(clock.UtcNow.AddDays(30), profile.Value.CanChangeUserNameAt);
        Assert.DoesNotContain("dateOfBirth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"age\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana@correo.com", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_adult_photo_is_cropped_and_a_minor_cannot_upload_one()
    {
        var wide = WithExif(Picture(900, 300, SKColors.Red, SKColors.Blue));
        var processor = new SkiaAvatarProcessor();
        var processed = processor.Process(wide, 5 * 1024 * 1024);
        Assert.True(processed.IsSuccess);
        Assert.DoesNotContain("GPSLATITUDE-SECRET", Encoding.ASCII.GetString(processed.Value!), StringComparison.Ordinal);
        using var decoded = SKBitmap.Decode(processed.Value);
        Assert.Equal(512, decoded.Width);
        Assert.Equal(512, decoded.Height);
        Assert.True(decoded.GetPixel(256, 256).Blue > decoded.GetPixel(256, 256).Red);

        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var adult = await UserAsync(db, clock, catalog, 30, "ana@situ.test", "ana");
        var minor = await UserAsync(db, clock, catalog, 16, "lia@situ.test", "lia");
        var files = new MemoryFiles();
        var gallery = AvatarGalleryItem.Create("Pin", "/assets/avatars/02.png", 2);
        gallery.MarkCreated(clock.UtcNow);
        db.AvatarGalleryItems.Add(gallery);
        await db.SaveChangesAsync();

        var rejected = await new UploadAvatarPhotoHandler(db, clock, files, processor)
            .Handle(new UploadAvatarPhotoRequest(minor.Id, wide), CancellationToken.None);
        Assert.Equal("avatar.minor_photo_not_allowed", rejected.Error!.Code);
        Assert.Empty(files.Saved);

        var uploaded = await new UploadAvatarPhotoHandler(db, clock, files, processor)
            .Handle(new UploadAvatarPhotoRequest(adult.Id, wide), CancellationToken.None);
        Assert.True(uploaded.IsSuccess);
        var custom = await db.UserProfiles.SingleAsync(item => item.UserId == adult.Id);
        Assert.Equal(AvatarKind.Custom, custom.AvatarKind);
        Assert.StartsWith("avatars/", custom.AvatarUrl);
        var previous = custom.AvatarUrl;

        var picked = await new SetGalleryAvatarHandler(db, clock, files)
            .Handle(new SetGalleryAvatarRequest(adult.Id, gallery.Id), CancellationToken.None);
        Assert.True(picked.IsSuccess);
        Assert.False(files.Saved.ContainsKey(previous!));
        var after = await db.UserProfiles.SingleAsync(item => item.UserId == adult.Id);
        Assert.Equal(AvatarKind.Gallery, after.AvatarKind);
        Assert.Equal(gallery.Id, after.AvatarGalleryItemId);
    }

    [Fact]
    public async Task Changing_the_password_locks_after_failures_and_closes_the_other_sessions()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var ana = await UserAsync(db, clock, catalog, 30, "ana@situ.test", "ana", "hashed:Clave123");
        var current = UserDevice.Register(ana.Id, "phone", DevicePlatform.Android, "Pixel", "1", null, clock.UtcNow);
        var other = UserDevice.Register(ana.Id, "web", DevicePlatform.Web, "Chrome", "1", null, clock.UtcNow);
        other.UpdatePushToken("ExponentPushToken[abc]");
        db.UserDevices.AddRange(current, other);
        db.RefreshTokens.Add(RefreshToken.Issue(ana.Id, current.Id, "current", clock.UtcNow, TimeSpan.FromDays(1)));
        db.RefreshTokens.Add(RefreshToken.Issue(ana.Id, other.Id, "other", clock.UtcNow, TimeSpan.FromDays(1)));
        db.SecurityTokens.Add(SecurityToken.Issue(ana.Id, SecurityTokenPurpose.PasswordReset, "reset", clock.UtcNow, TimeSpan.FromHours(1)));
        db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(ana.Id, other.Id, 4.711, -74.072, 20, clock.UtcNow, TimeSpan.FromMinutes(30)));
        await db.SaveChangesAsync();

        var mail = new FakeEmailSender();
        var handler = new ChangePasswordHandler(db, clock, new FakePasswords(), new CurrentUser(ana.Id, current.Id), mail);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var wrong = await handler.Handle(new ChangePasswordRequest("Mala1234", "Nueva1234"), CancellationToken.None);
            Assert.Equal("password.current_invalid", wrong.Error!.Code);
        }

        var locked = await handler.Handle(new ChangePasswordRequest("Mala1234", "Nueva1234"), CancellationToken.None);
        Assert.Equal("auth.locked", locked.Error!.Code);
        Assert.Equal("hashed:Clave123", (await db.Users.SingleAsync()).PasswordHash);

        clock.UtcNow = clock.UtcNow.AddMinutes(16);
        var changed = await handler.Handle(new ChangePasswordRequest("Clave123", "Nueva1234", true), CancellationToken.None);
        Assert.True(changed.IsSuccess);
        Assert.Equal("hashed:Nueva1234", (await db.Users.SingleAsync()).PasswordHash);
        Assert.Equal("ana@situ.test", mail.Messages.Single().To);
        Assert.DoesNotContain("Nueva1234", mail.Messages.Single().Body, StringComparison.Ordinal);
        Assert.NotNull((await db.SecurityTokens.SingleAsync()).ConsumedAt);
        Assert.Null((await db.UserDevices.SingleAsync(item => item.Id == current.Id)).RevokedAt);
        Assert.NotNull((await db.UserDevices.SingleAsync(item => item.Id == other.Id)).RevokedAt);
        Assert.Null((await db.UserDevices.SingleAsync(item => item.Id == other.Id)).PushToken);
        Assert.Empty(await db.DeviceAlertLocations.ToListAsync());
        Assert.Null((await db.RefreshTokens.SingleAsync(item => item.UserDeviceId == current.Id)).RevokedAt);
        Assert.NotNull((await db.RefreshTokens.SingleAsync(item => item.UserDeviceId == other.Id)).RevokedAt);
    }

    [Fact]
    public async Task Devices_can_be_listed_and_only_another_device_can_be_revoked()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var ana = await UserAsync(db, clock, catalog, 30, "ana@situ.test", "ana");
        var current = UserDevice.Register(ana.Id, "phone", DevicePlatform.IOS, "iPhone", "1", null, clock.UtcNow);
        var other = UserDevice.Register(ana.Id, "web", DevicePlatform.Web, "Chrome", "1", null, clock.UtcNow);
        other.UpdatePushToken("token");
        db.UserDevices.AddRange(current, other);
        db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(ana.Id, other.Id, 4.7, -74.0, null, clock.UtcNow, TimeSpan.FromMinutes(30)));
        await db.SaveChangesAsync();

        var actor = new CurrentUser(ana.Id, current.Id);
        var listed = await new ListMyDevicesHandler(db, actor).Handle(new ListMyDevicesRequest(), CancellationToken.None);
        Assert.Contains(listed.Value!.Devices, item => item.Id == current.Id && item.IsCurrent && item.DeviceName == "iPhone");
        Assert.Contains(listed.Value.Devices, item => item.Id == other.Id && !item.IsCurrent);

        var refused = await new RevokeDeviceHandler(db, clock, actor).Handle(new RevokeDeviceRequest(current.Id), CancellationToken.None);
        Assert.Equal("auth.current_device", refused.Error!.Code);
        Assert.Null((await db.UserDevices.SingleAsync(item => item.Id == current.Id)).RevokedAt);

        var revoked = await new RevokeDeviceHandler(db, clock, actor).Handle(new RevokeDeviceRequest(other.Id), CancellationToken.None);
        Assert.True(revoked.IsSuccess);
        Assert.NotNull((await db.UserDevices.SingleAsync(item => item.Id == other.Id)).RevokedAt);
        Assert.Null((await db.UserDevices.SingleAsync(item => item.Id == other.Id)).PushToken);
        Assert.Empty(await db.DeviceAlertLocations.ToListAsync());

        var again = await new RevokeDeviceHandler(db, clock, actor).Handle(new RevokeDeviceRequest(other.Id), CancellationToken.None);
        Assert.True(again.IsSuccess);
    }

    private static Task<Result> Update(IdentityTestDb db, FixedClock clock, Guid userId, UpdateMyProfileRequest request) =>
        new UpdateMyProfileHandler(db, clock).Handle(request, CancellationToken.None);

    private static async Task<User> UserAsync(
        IdentityTestDb db,
        FixedClock clock,
        Catalog catalog,
        int years,
        string email,
        string userName,
        string passwordHash = "hash")
    {
        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("3001234567"),
            passwordHash,
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(-years),
            catalog.CountryId,
            catalog.RegionId,
            catalog.CityId,
            "es-CO",
            14,
            clock.UtcNow);
        user.ConfirmEmail(clock.UtcNow);
        if (user.IsMinor(clock.UtcNow))
        {
            user.GrantGuardianConsent(clock.UtcNow);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static byte[] Picture(int width, int height, SKColor fill, SKColor center)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(fill);
        var side = Math.Min(width, height);
        var left = (width - side) / 2f;
        var top = (height - side) / 2f;
        canvas.DrawRect(new SKRect(left, top, left + side, top + side), new SKPaint { Color = center });
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private static byte[] WithExif(byte[] jpeg)
    {
        var marker = Encoding.ASCII.GetBytes("GPSLATITUDE-SECRET");
        var segment = new byte[4 + marker.Length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        var length = marker.Length + 2;
        segment[2] = (byte)(length >> 8);
        segment[3] = (byte)length;
        marker.CopyTo(segment.AsSpan(4));
        var result = new byte[jpeg.Length + segment.Length];
        result[0] = jpeg[0];
        result[1] = jpeg[1];
        segment.CopyTo(result.AsSpan(2));
        jpeg.AsSpan(2).CopyTo(result.AsSpan(2 + segment.Length));
        return result;
    }

    private sealed class CurrentUser(Guid userId, Guid? deviceId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public Guid? DeviceId { get; } = deviceId;
        public bool IsAuthenticated => true;
    }

    private sealed class Signer : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (expiresAt.ToUnixTimeSeconds(), "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => signature == "sig";
    }

    private sealed class MemoryFiles : IFileStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = [];

        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
        {
            Saved[key] = content;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Saved.Remove(key);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(Saved.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);
    }
}

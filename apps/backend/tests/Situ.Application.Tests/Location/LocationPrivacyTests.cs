using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction.AccountDeletion;
using Situ.Application.Features.Interaction.Blocks;
using Situ.Application.Features.Location;
using Situ.Application.Features.Social.FriendRequests;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Social;

namespace Situ.Application.Tests.Location;

public class LocationPrivacyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 15, 7, 0, TimeSpan.Zero);
    private const double Lat = 4.598173;
    private const double Lon = -74.081928;
    private const string Consent = "2026-10-27";

    [Fact]
    public async Task A_non_friend_never_receives_a_location_and_the_point_is_rounded()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "owner@situ.test", "duena");
        var friend = await UserAsync(db, clock, 29, "friend@situ.test", "amigo");
        var stranger = await UserAsync(db, clock, 31, "stranger@situ.test", "ajeno");
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "AllFriends", "until_off", null);

        var posted = await PostAsync(db, clock, locations, owner.Id);
        Assert.True(posted.Value!.Live);
        Assert.Contains(locations.Events, item => item.Viewer == friend.Id && item.Kind == "update");
        Assert.DoesNotContain(locations.Events, item => item.Viewer == stranger.Id);
        var stored = await db.UserLocations.SingleAsync(item => item.UserId == owner.Id);
        var rounded = LocationGrid.Round(new GeoPoint(Lat, Lon), 25);
        Assert.Equal(rounded.Latitude, stored.Point.Latitude);
        Assert.NotEqual(Lat, stored.Point.Latitude);
        Assert.All(db.AuditLogs.Where(item => item.Action == "location.post"), item => Assert.Null(item.Changes));
        Assert.DoesNotContain(db.Notifications, item => item.Body.Contains("4.598", StringComparison.Ordinal));
        Assert.DoesNotContain(db.AuditLogs, item => item.Changes != null && item.Changes.Contains("4.598", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Blocking_or_removing_a_friend_stops_delivery_immediately()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "owner2@situ.test", "duena2");
        var friend = await UserAsync(db, clock, 29, "friend2@situ.test", "amigo2");
        var other = await UserAsync(db, clock, 28, "other2@situ.test", "otro2");
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        db.Friendships.Add(Accepted(owner.Id, other.Id));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "AllFriends", "until_off", null);
        await PostAsync(db, clock, locations, owner.Id);

        await new BlockUserHandler(db, clock, locations).Handle(new BlockUserRequest(friend.Id, owner.Id), CancellationToken.None);
        locations.Events.Clear();
        await PostAsync(db, clock, locations, owner.Id);
        Assert.DoesNotContain(locations.Events, item => item.Viewer == friend.Id && item.Kind == "update");
        Assert.Contains(locations.Events, item => item.Viewer == other.Id && item.Kind == "update");

        await new RemoveFriendHandler(db, clock, locations).Handle(new RemoveFriendRequest(owner.Id, other.Id), CancellationToken.None);
        locations.Events.Clear();
        var after = await PostAsync(db, clock, locations, owner.Id);
        Assert.True(after.Value!.Live);
        Assert.DoesNotContain(locations.Events, item => item.Viewer == other.Id);
        Assert.Contains(db.UserLocations, item => item.UserId == owner.Id);
    }

    [Fact]
    public async Task A_selected_list_delivers_only_to_the_chosen_friends()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "owner3@situ.test", "duena3");
        var chosen = await UserAsync(db, clock, 29, "chosen@situ.test", "elegido");
        var skipped = await UserAsync(db, clock, 28, "skipped@situ.test", "omitido");
        db.Friendships.Add(Accepted(owner.Id, chosen.Id));
        db.Friendships.Add(Accepted(owner.Id, skipped.Id));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "SelectedFriends", "8h", [chosen.Id, skipped.Id, Guid.CreateVersion7()]);
        await ShareAsync(db, clock, locations, owner.Id, "SelectedFriends", "8h", [chosen.Id]);

        await PostAsync(db, clock, locations, owner.Id);
        Assert.Contains(locations.Events, item => item.Viewer == chosen.Id && item.Kind == "update");
        Assert.DoesNotContain(locations.Events, item => item.Viewer == skipped.Id && item.Kind == "update");
    }

    [Fact]
    public async Task Sharing_expires_on_its_own_and_the_last_position_is_deleted()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "owner4@situ.test", "duena4");
        var friend = await UserAsync(db, clock, 29, "friend4@situ.test", "amigo4");
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "AllFriends", "1h", null);
        await PostAsync(db, clock, locations, owner.Id);
        clock.UtcNow = Now.AddHours(2);

        var expired = await PostAsync(db, clock, locations, owner.Id);
        Assert.Equal("location.sharing_off", expired.Error!.Code);
        Assert.DoesNotContain(db.UserLocations, item => item.UserId == owner.Id);
        Assert.Contains(locations.Events, item => item.Viewer == friend.Id && item.Kind == "removed");
        var view = await new GetLocationSharingHandler(db, clock, locations).Handle(new GetLocationSharingRequest(owner.Id), CancellationToken.None);
        Assert.False(view.Value!.Enabled);
    }

    [Fact]
    public async Task A_hidden_zone_drops_the_point_and_an_approximate_zone_coarsens_it()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "owner5@situ.test", "duena5");
        var friend = await UserAsync(db, clock, 29, "friend5@situ.test", "amigo5");
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "AllFriends", "until_off", null);
        var zones = new SavePrivacyZoneHandler(db, clock);
        await zones.Handle(new SavePrivacyZoneRequest(owner.Id, null, "Casa", Lat, Lon, 150, "Hidden"), CancellationToken.None);

        var hidden = await PostAsync(db, clock, locations, owner.Id);
        Assert.True(hidden.Value!.HiddenByZone);
        Assert.False(hidden.Value.Live);
        Assert.DoesNotContain(db.UserLocations, item => item.UserId == owner.Id);
        Assert.DoesNotContain(locations.Events, item => item.Kind == "update");

        var casa = await db.PrivacyZones.SingleAsync(item => item.Name == "Casa");
        await new DeletePrivacyZoneHandler(db).Handle(new DeletePrivacyZoneRequest(owner.Id, casa.Id), CancellationToken.None);
        await zones.Handle(new SavePrivacyZoneRequest(owner.Id, null, "Trabajo", Lat, Lon, 800, "Approximate"), CancellationToken.None);
        var coarse = await PostAsync(db, clock, locations, owner.Id);
        Assert.True(coarse.Value!.Live);
        var stored = await db.UserLocations.SingleAsync(item => item.UserId == owner.Id);
        Assert.Equal(LocationGrid.Round(new GeoPoint(Lat, Lon), 800).Latitude, stored.Point.Latitude);
    }

    [Fact]
    public async Task A_minor_cannot_share_without_a_guardian_or_appear_on_the_public_map()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var minor = await UserAsync(db, clock, 15, "minor@situ.test", "menor");
        var locations = new RecordingLocations();
        var sharing = new SetLocationSharingHandler(db, clock, locations);
        var denied = await sharing.Handle(new SetLocationSharingRequest(minor.Id, true, "AllFriends", "until_off", null, Consent, null), CancellationToken.None);
        Assert.Equal("location.guardian_required", denied.Error!.Code);

        var consent = GuardianConsent.Request(minor.Id, "tutor@situ.test", "hash", "privacy-2026", Now.AddMinutes(-1), Now);
        consent.SubmitGuardianData("Carlos Pérez", GuardianDocumentType.CitizenshipCard, "1234567890", "carlos@correo.com", Now);
        consent.VerifyGuardianEmail(Now);
        consent.Authorize("signature", false, false, "127.0.0.1", "SituTests", Now);
        db.GuardianConsents.Add(consent);
        await db.SaveChangesAsync();
        var stillDenied = await sharing.Handle(new SetLocationSharingRequest(minor.Id, true, "AllFriends", "until_off", null, Consent, null), CancellationToken.None);
        Assert.Equal("location.guardian_required", stillDenied.Error!.Code);

        consent.UpdatePermissions(false, true, Now);
        await db.SaveChangesAsync();
        var allowed = await sharing.Handle(new SetLocationSharingRequest(minor.Id, true, "AllFriends", "until_off", null, Consent, null), CancellationToken.None);
        Assert.True(allowed.Value!.Enabled);
        Assert.True(allowed.Value.ReminderDue == false);

        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adulta");
        var profile = UserProfile.CreateDefault(adult.Id, "Adulta");
        profile.MakePublic(30, Now, Consent);
        profile.MarkCreated(Now);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        var publication = new SetPublicLocationHandler(db, clock, locations);
        var minorPublic = await publication.Handle(new SetPublicLocationRequest(minor.Id, "Realtime", Consent), CancellationToken.None);
        Assert.Equal("location.minor_public", minorPublic.Error!.Code);
        var adultPublic = await publication.Handle(new SetPublicLocationRequest(adult.Id, "Realtime", Consent), CancellationToken.None);
        Assert.Equal("Realtime", adultPublic.Value!.PublicMode);
        await PostAsync(db, clock, locations, adult.Id);
        var people = await new GetMapPeopleHandler(db, clock, new FakeLinks(), new Signer())
            .Handle(new GetMapPeopleRequest(minor.Id, 4.4, -74.3, 4.8, -73.9), CancellationToken.None);
        Assert.Contains(people.Value!.People, item => item.UserId == adult.Id);
        Assert.DoesNotContain(people.Value.People, item => item.UserId == minor.Id);
        var shown = people.Value.People.Single(item => item.UserId == adult.Id);
        Assert.Equal(LocationGrid.Round(new GeoPoint(Lat, Lon), 300).Latitude, shown.Latitude);
    }

    [Fact]
    public async Task An_anonymous_publication_removes_the_avatar_from_friends_and_the_public_map()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "anon@situ.test", "anonima");
        var friend = await UserAsync(db, clock, 29, "friend6@situ.test", "amigo6");
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        db.Reports.Add(Report.Create(owner.Id, Guid.NewGuid(), owner.CityId, new GeoPoint(Lat, Lon), "Anónimo.", true, false, TimeSpan.FromHours(2), Now));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "AllFriends", "until_off", null);
        var profile = await db.UserProfiles.SingleAsync(item => item.UserId == owner.Id);
        profile.MakePublic(30, Now, Consent);
        await db.SaveChangesAsync();
        await new SetPublicLocationHandler(db, clock, locations).Handle(new SetPublicLocationRequest(owner.Id, "Realtime", Consent), CancellationToken.None);

        var posted = await PostAsync(db, clock, locations, owner.Id);
        Assert.True(posted.Value!.HiddenBecauseAnonymous);
        Assert.DoesNotContain(db.UserLocations, item => item.UserId == owner.Id);
        Assert.DoesNotContain(locations.Events, item => item.Kind == "update");
        var people = await new GetMapPeopleHandler(db, clock, new FakeLinks(), new Signer())
            .Handle(new GetMapPeopleRequest(friend.Id, 4.4, -74.3, 4.8, -73.9), CancellationToken.None);
        Assert.DoesNotContain(people.Value!.People, item => item.UserId == owner.Id);
    }

    [Fact]
    public async Task Posts_are_limited_stale_readings_are_rejected_and_pause_rounds_last_seen()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "rate@situ.test", "rapida");
        var friend = await UserAsync(db, clock, 29, "rate-friend@situ.test", "amigorate");
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.LocationPostsPerMinute, "2", null, Now));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "AllFriends", "until_off", null);
        Assert.True((await PostAsync(db, clock, locations, owner.Id)).IsSuccess);
        Assert.True((await PostAsync(db, clock, locations, owner.Id)).IsSuccess);
        var limited = await PostAsync(db, clock, locations, owner.Id);
        Assert.Equal("location.rate_limited", limited.Error!.Code);

        var stale = await new PostLocationHandler(db, clock, locations, new FakeLinks(), new Signer())
            .Handle(new PostLocationRequest(owner.Id, Lat, Lon, 20, Now.AddMinutes(-5)), CancellationToken.None);
        Assert.Equal("location.reading_rejected", stale.Error!.Code);
        var inaccurate = await new PostLocationHandler(db, clock, locations, new FakeLinks(), new Signer())
            .Handle(new PostLocationRequest(owner.Id, Lat, Lon, 900, Now), CancellationToken.None);
        Assert.Equal("location.reading_rejected", inaccurate.Error!.Code);

        clock.UtcNow = Now.AddMinutes(2);
        var paused = await new PauseLocationHandler(db, clock, locations, new FakeLinks(), new Signer())
            .Handle(new PauseLocationRequest(owner.Id), CancellationToken.None);
        Assert.False(paused.Value!.Live);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 15, 5, 0, TimeSpan.Zero), paused.Value.LastSeenAt);
        Assert.Contains(locations.Events, item => item.Viewer == friend.Id && item.Kind == "update" && item.Live == false);
    }

    [Fact]
    public async Task Purge_and_account_deletion_remove_positions_zones_and_consent()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "erase-loc@situ.test", "borrarloc");
        var friend = await UserAsync(db, clock, 29, "erase-friend@situ.test", "amigoborra");
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        await db.SaveChangesAsync();
        var locations = new RecordingLocations();
        await ShareAsync(db, clock, locations, owner.Id, "AllFriends", "until_off", null);
        await PostAsync(db, clock, locations, owner.Id);
        await new SavePrivacyZoneHandler(db, clock).Handle(new SavePrivacyZoneRequest(owner.Id, null, "Casa", Lat + 1, Lon, 100, "Hidden"), CancellationToken.None);
        clock.UtcNow = Now.AddMinutes(20);
        var purged = await new PurgeUserLocationsHandler(db, clock, locations).Handle(new PurgeUserLocationsRequest(), CancellationToken.None);
        Assert.Equal(1, purged.Value!.Removed);
        Assert.DoesNotContain(db.UserLocations, item => item.UserId == owner.Id);
        Assert.Contains(locations.Events, item => item.Viewer == friend.Id && item.Kind == "removed");

        await PostAsync(db, clock, locations, owner.Id);
        var requested = await new RequestAccountDeletionHandler(db, clock, new PlainHasher(), new FakeEmailSender())
            .Handle(new RequestAccountDeletionRequest(owner.Id, "hash"), CancellationToken.None);
        clock.UtcNow = requested.Value!.ScheduledFor.AddDays(14);
        var done = await new CompleteAccountDeletionsHandler(db, clock, new MemoryFiles(), locations)
            .Handle(new CompleteAccountDeletionsRequest(), CancellationToken.None);
        Assert.Equal(1, done.Value!.Completed);
        Assert.DoesNotContain(db.UserLocations, item => item.UserId == owner.Id);
        Assert.DoesNotContain(db.PrivacyZones, item => item.UserId == owner.Id);
        Assert.DoesNotContain(db.LocationShareRecipients, item => item.UserId == owner.Id || item.FriendUserId == owner.Id);
        var erased = await db.UserProfiles.SingleAsync(item => item.UserId == owner.Id);
        Assert.False(erased.ShareLocationWithFriends);
        Assert.Null(erased.FriendLocationConsentedAt);
        Assert.Equal(PublicLocationMode.Hidden, erased.PublicLocationMode);
    }

    private static async Task<Result<PostLocationResponse>> PostAsync(IdentityTestDb db, FixedClock clock, RecordingLocations locations, Guid userId) =>
        await new PostLocationHandler(db, clock, locations, new FakeLinks(), new Signer())
            .Handle(new PostLocationRequest(userId, Lat, Lon, 20, clock.UtcNow), CancellationToken.None);

    private static async Task ShareAsync(IdentityTestDb db, FixedClock clock, RecordingLocations locations, Guid userId, string mode, string duration, Guid[]? friendIds)
    {
        var result = await new SetLocationSharingHandler(db, clock, locations)
            .Handle(new SetLocationSharingRequest(userId, true, mode, duration, friendIds, Consent, null), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    private static Friendship Accepted(Guid left, Guid right)
    {
        var friendship = Friendship.Request(left, right, Now);
        friendship.Accept(right, Now);
        return friendship;
    }

    private static async Task<User> UserAsync(IdentityTestDb db, FixedClock clock, int years, string email, string userName)
    {
        var city = await db.Cities.Select(item => new { item.Id, item.RegionId, item.CountryId }).FirstOrDefaultAsync();
        Guid countryId;
        Guid regionId;
        Guid cityId;
        if (city is null)
        {
            var country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
            var region = Region.Create(country.Id, "Cundinamarca", "25");
            var created = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
            db.AddRange(country, region, created);
            await db.SaveChangesAsync();
            countryId = country.Id;
            regionId = region.Id;
            cityId = created.Id;
        }
        else
        {
            countryId = city.CountryId;
            regionId = city.RegionId;
            cityId = city.Id;
        }

        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            null,
            "hash",
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(-years),
            countryId,
            regionId,
            cityId,
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

    private sealed class RecordingLocations : IFriendLocationPublisher
    {
        public List<(Guid Viewer, string Kind, Guid Subject, double? Latitude, bool Live)> Events { get; } = [];

        public Task UpdateAsync(Guid viewerId, FriendLocationPush push, CancellationToken cancellationToken)
        {
            Events.Add((viewerId, "update", push.UserId, push.Latitude, push.Live));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid viewerId, Guid subjectUserId, CancellationToken cancellationToken)
        {
            Events.Add((viewerId, "removed", subjectUserId, null, false));
            return Task.CompletedTask;
        }
    }

    private sealed class PlainHasher : IPasswordHasher
    {
        public string Hash(string password) => password;

        public bool Verify(string password, string passwordHash) => password == passwordHash;
    }

    private sealed class MemoryFiles : IFileStorage
    {
        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
    }

    private sealed class Signer : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (expiresAt.ToUnixTimeSeconds(), "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => signature == "sig";
    }
}

using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Interaction.AccountDeletion;
using Situ.Application.Features.Interaction.Blocks;
using Situ.Application.Features.Profiles.ProfileVisibility;
using Situ.Application.Features.Reports.GetReportMap;
using Situ.Application.Features.Social.FriendRequests;
using Situ.Application.Features.Social.GetPublicProfile;
using Situ.Application.Features.Social.InviteFriend;
using Situ.Application.Features.Social.SearchUsers;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Social;
using Situ.Infrastructure.Reports;

namespace Situ.Application.Tests.Social;

public class FriendsPrivacyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_adult_cannot_find_request_or_invite_a_minor_and_a_minor_is_absent_from_listings()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adulta");
        var minor = await UserAsync(db, clock, 15, "minor@situ.test", "menor");
        var otherMinor = await UserAsync(db, clock, 16, "other-minor@situ.test", "menor2");
        db.Friendships.Add(Accepted(adult.Id, minor.Id));
        await db.SaveChangesAsync();

        var search = new SearchUsersHandler(db, clock, new FakeLinks(), new Signer());
        var hidden = await search.Handle(new SearchUsersRequest(adult.Id, "menor"), CancellationToken.None);
        Assert.Empty(hidden.Value!.Users);
        var found = await search.Handle(new SearchUsersRequest(otherMinor.Id, "MENOR"), CancellationToken.None);
        Assert.Equal(minor.Id, Assert.Single(found.Value!.Users).UserId);
        Assert.Equal("menor", found.Value.Users[0].UserName);

        var send = new SendFriendRequestHandler(db, clock);
        var request = await send.Handle(new SendFriendRequestRequest(adult.Id, minor.Id), CancellationToken.None);
        Assert.Equal("sent", request.Value!.Status);
        Assert.DoesNotContain(db.Friendships, item => item.Status == FriendshipStatus.Pending);

        var mail = new FakeEmailSender();
        var invite = await new InviteFriendByEmailHandler(db, clock, mail, new FakeLinks())
            .Handle(new InviteFriendByEmailRequest(adult.Id, minor.Email.Value), CancellationToken.None);
        Assert.Equal("sent", invite.Value!.Status);
        Assert.Empty(mail.Messages);

        var listed = await new ListFriendsHandler(db, clock, new FakeLinks(), new Signer())
            .Handle(new ListFriendsRequest(adult.Id), CancellationToken.None);
        Assert.Empty(listed.Value!.Friends);
    }

    [Fact]
    public async Task Search_is_exact_and_stops_after_the_configured_limit()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adulta");
        await UserAsync(db, clock, 28, "other@situ.test", "adulto");
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.FriendsSearchPerMinute, "2", null, Now));
        await db.SaveChangesAsync();
        var search = new SearchUsersHandler(db, clock, new FakeLinks(), new Signer());

        Assert.False(new SearchUsersValidator().Validate(new SearchUsersRequest(adult.Id, "ab")).IsValid);
        var exact = await search.Handle(new SearchUsersRequest(adult.Id, "Adulto"), CancellationToken.None);
        Assert.Equal("adulto", Assert.Single(exact.Value!.Users).UserName);
        var partial = await search.Handle(new SearchUsersRequest(adult.Id, "adul"), CancellationToken.None);
        Assert.Empty(partial.Value!.Users);
        var limited = await search.Handle(new SearchUsersRequest(adult.Id, "adulto"), CancellationToken.None);
        Assert.Equal("friendship.search_limited", limited.Error!.Code);
        Assert.All(db.AuditLogs.Where(item => item.Action == "friends.search"), item => Assert.Null(item.Changes));
        Assert.Equal("none", exact.Value.Users[0].Relation);
    }

    [Fact]
    public async Task Search_reports_the_tie_and_history_lists_only_the_chosen_status()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var me = await UserAsync(db, clock, 30, "me@situ.test", "yoyo");
        var sent = await UserAsync(db, clock, 30, "sent@situ.test", "enviada");
        var got = await UserAsync(db, clock, 30, "got@situ.test", "recibida");
        var pal = await UserAsync(db, clock, 30, "pal@situ.test", "amigo");
        var no = await UserAsync(db, clock, 30, "no@situ.test", "rechazo");
        var outgoing = Friendship.Request(me.Id, sent.Id, Now);
        var incoming = Friendship.Request(got.Id, me.Id, Now);
        var accepted = Friendship.Request(me.Id, pal.Id, Now);
        accepted.Accept(pal.Id, Now);
        var declined = Friendship.Request(me.Id, no.Id, Now);
        declined.Decline(no.Id, Now);
        db.Friendships.AddRange(outgoing, incoming, accepted, declined);
        await db.SaveChangesAsync();
        var search = new SearchUsersHandler(db, clock, new FakeLinks(), new Signer());

        Assert.Equal("outgoing", (await search.Handle(new SearchUsersRequest(me.Id, "enviada"), CancellationToken.None)).Value!.Users[0].Relation);
        Assert.Equal("incoming", (await search.Handle(new SearchUsersRequest(me.Id, "recibida"), CancellationToken.None)).Value!.Users[0].Relation);
        Assert.Equal("friends", (await search.Handle(new SearchUsersRequest(me.Id, "amigo"), CancellationToken.None)).Value!.Users[0].Relation);

        var history = new ListFriendHistoryHandler(db, clock, new FakeLinks(), new Signer());
        var rejected = await history.Handle(new ListFriendHistoryRequest(me.Id, "Declined"), CancellationToken.None);
        Assert.Equal(no.Id, Assert.Single(rejected.Value!.Items).UserId);
        var pending = await history.Handle(new ListFriendHistoryRequest(me.Id, "pending"), CancellationToken.None);
        Assert.Equal(2, pending.Value!.Items.Count);
        Assert.Equal("friendship.history_filter", (await history.Handle(new ListFriendHistoryRequest(me.Id, "cancelled"), CancellationToken.None)).Error!.Code);
    }

    [Fact]
    public async Task A_private_profile_is_indistinguishable_from_a_missing_one_and_a_block_cuts_the_friendship()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "owner@situ.test", "duena");
        var friend = await UserAsync(db, clock, 29, "friend@situ.test", "amigo");
        var stranger = await UserAsync(db, clock, 31, "stranger@situ.test", "ajeno");
        var profile = UserProfile.CreateDefault(owner.Id, "Dueña");
        profile.UpdateDetails("Dueña", "Bio privada");
        profile.MarkCreated(Now);
        db.UserProfiles.Add(profile);
        var friendship = Accepted(owner.Id, friend.Id);
        db.Friendships.Add(friendship);
        db.LocationShareRecipients.Add(LocationShareRecipient.Create(owner.Id, friend.Id));
        await db.SaveChangesAsync();

        var profiles = new GetPublicProfileHandler(db, clock, new FakeLinks(), new Signer());
        var missing = await profiles.Handle(new GetPublicProfileRequest(stranger.Id, "nadie"), CancellationToken.None);
        var hidden = await profiles.Handle(new GetPublicProfileRequest(stranger.Id, "duena"), CancellationToken.None);
        Assert.Equal("profile.not_available", missing.Error!.Code);
        Assert.Equal(missing.Error.Code, hidden.Error!.Code);
        Assert.Equal("No encontramos ese perfil.", missing.Error.Message);
        Assert.Equal(missing.Error.Message, hidden.Error.Message);
        var asFriend = await profiles.Handle(new GetPublicProfileRequest(friend.Id, "duena"), CancellationToken.None);
        Assert.Equal("Bio privada", asFriend.Value!.Bio);

        var removed = await new RemoveFriendHandler(db, clock).Handle(new RemoveFriendRequest(owner.Id, friend.Id), CancellationToken.None);
        Assert.True(removed.IsSuccess);
        Assert.Equal(FriendshipStatus.Cancelled, friendship.Status);
        Assert.Empty(db.LocationShareRecipients);

        profile.MakePublic(30, Now, "2026-10-27");
        db.Friendships.Add(Accepted(owner.Id, stranger.Id));
        db.LocationShareRecipients.Add(LocationShareRecipient.Create(owner.Id, stranger.Id));
        await db.SaveChangesAsync();
        var visible = await profiles.Handle(new GetPublicProfileRequest(stranger.Id, "duena"), CancellationToken.None);
        Assert.Equal("Bio privada", visible.Value!.Bio);
        await new BlockUserHandler(db, clock).Handle(new BlockUserRequest(owner.Id, stranger.Id), CancellationToken.None);
        Assert.DoesNotContain(db.Friendships, item => item.Status == FriendshipStatus.Accepted && item.Involves(stranger.Id));
        Assert.Empty(db.LocationShareRecipients);
        var blocked = await profiles.Handle(new GetPublicProfileRequest(stranger.Id, "duena"), CancellationToken.None);
        Assert.Equal("No encontramos ese perfil.", blocked.Error!.Message);
    }

    [Fact]
    public async Task An_email_invitation_answers_the_same_way_whether_or_not_the_account_exists()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adulta");
        var known = await UserAsync(db, clock, 28, "known@situ.test", "conocido");
        var mail = new FakeEmailSender();
        var handler = new InviteFriendByEmailHandler(db, clock, mail, new FakeLinks());

        var missing = await handler.Handle(new InviteFriendByEmailRequest(adult.Id, "nuevo@situ.test"), CancellationToken.None);
        var present = await handler.Handle(new InviteFriendByEmailRequest(adult.Id, known.Email.Value), CancellationToken.None);
        Assert.Equal("sent", missing.Value!.Status);
        Assert.Equal(missing.Value.Status, present.Value!.Status);
        Assert.Contains(mail.Messages, item => item.To == "nuevo@situ.test" && item.Body.Contains("token=", StringComparison.Ordinal));
        Assert.Contains(db.Friendships, item => item.RequesterId == adult.Id && item.AddresseeId == known.Id && item.Status == FriendshipStatus.Pending);

        var token = Uri.UnescapeDataString(mail.Messages.Single(item => item.To == "nuevo@situ.test").Body.Split("token=")[1].Trim());
        var invited = await UserAsync(db, clock, 27, "nuevo@situ.test", "nuevo");
        var accepted = await new AcceptFriendInvitationHandler(db, clock)
            .Handle(new AcceptFriendInvitationRequest(invited.Id, token), CancellationToken.None);
        Assert.Equal("pending", accepted.Value!.Status);
        Assert.Contains(db.Friendships, item => item.RequesterId == adult.Id && item.AddresseeId == invited.Id && item.Status == FriendshipStatus.Pending);
        var again = await new AcceptFriendInvitationHandler(db, clock)
            .Handle(new AcceptFriendInvitationRequest(invited.Id, token), CancellationToken.None);
        Assert.Equal("invitation.not_valid", again.Error!.Code);
    }

    [Fact]
    public async Task Account_deletion_removes_friendships_requests_and_invitations()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var user = await UserAsync(db, clock, 30, "erase@situ.test", "borrar");
        var other = await UserAsync(db, clock, 29, "other@situ.test", "otro");
        db.Friendships.Add(Accepted(user.Id, other.Id));
        db.Friendships.Add(Friendship.Request(other.Id, user.Id, Now));
        db.FriendInvitations.Add(FriendInvitation.Create(user.Id, "pending@situ.test", "hash-1", Now));
        db.FriendInvitations.Add(FriendInvitation.Create(other.Id, user.Email.Value, "hash-2", Now));
        await db.SaveChangesAsync();

        var requested = await new RequestAccountDeletionHandler(db, clock, new PlainHasher(), new FakeEmailSender())
            .Handle(new RequestAccountDeletionRequest(user.Id, "hash"), CancellationToken.None);
        clock.UtcNow = requested.Value!.ScheduledFor.AddDays(14);
        var done = await new CompleteAccountDeletionsHandler(db, clock, new MemoryFiles())
            .Handle(new CompleteAccountDeletionsRequest(), CancellationToken.None);
        Assert.Equal(1, done.Value!.Completed);
        Assert.DoesNotContain(db.Friendships, item => item.Involves(user.Id));
        Assert.DoesNotContain(db.FriendInvitations, item => item.InviterId == user.Id || item.Email == "erase@situ.test");
    }

    [Fact]
    public async Task Going_public_requires_the_current_consent_and_a_minor_is_rejected()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adulta");
        var minor = await UserAsync(db, clock, 15, "minor@situ.test", "menor");
        var handler = new SetProfileVisibilityHandler(db, clock, new FakeLinks(), new Signer());

        var missingConsent = await handler.Handle(new SetProfileVisibilityRequest(adult.Id, true, null, null, null), CancellationToken.None);
        Assert.Equal("profile.consent_required", missingConsent.Error!.Code);
        var wrongVersion = await handler.Handle(new SetProfileVisibilityRequest(adult.Id, true, "1999-01-01", null, null), CancellationToken.None);
        Assert.Equal("profile.consent_required", wrongVersion.Error!.Code);
        var minorResult = await handler.Handle(new SetProfileVisibilityRequest(minor.Id, true, "2026-10-27", null, null), CancellationToken.None);
        Assert.Equal("profile.minor_must_be_private", minorResult.Error!.Code);

        var published = await handler.Handle(new SetProfileVisibilityRequest(adult.Id, true, "2026-10-27", false, true), CancellationToken.None);
        Assert.True(published.Value!.Public);
        Assert.Equal("2026-10-27", published.Value.ConsentVersion);
        Assert.NotNull(published.Value.StrangerPreview);
        Assert.Null(published.Value.StrangerPreview!.Bio);
        var stored = await db.UserProfiles.SingleAsync(item => item.UserId == adult.Id);
        Assert.Equal(ProfileVisibility.Public, stored.Visibility);
        Assert.NotNull(stored.PublicProfileConsentedAt);

        var hidden = await handler.Handle(new SetProfileVisibilityRequest(adult.Id, false, null, null, null), CancellationToken.None);
        Assert.False(hidden.Value!.Public);
        Assert.Null(hidden.Value.StrangerPreview);
    }

    [Fact]
    public async Task A_public_profile_never_includes_anonymous_reports()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await UserAsync(db, clock, 30, "owner@situ.test", "duena");
        var stranger = await UserAsync(db, clock, 31, "stranger@situ.test", "ajeno");
        var friend = await UserAsync(db, clock, 29, "friend@situ.test", "amigo");
        var outsider = await UserAsync(db, clock, 32, "out@situ.test", "afuera");
        var profile = UserProfile.CreateDefault(owner.Id, "Dueña");
        profile.UpdateDetails("Dueña", "Bio pública");
        profile.MakePublic(30, Now, "2026-10-27");
        profile.MarkCreated(Now);
        db.UserProfiles.Add(profile);
        db.Reports.Add(Report.Create(owner.Id, Guid.NewGuid(), owner.CityId, new GeoPoint(4.711, -74.072), "Reporte con nombre.", false, false, TimeSpan.FromHours(2), Now));
        db.Reports.Add(Report.Create(owner.Id, Guid.NewGuid(), owner.CityId, new GeoPoint(4.712, -74.073), "Reporte anónimo secreto.", true, false, TimeSpan.FromHours(2), Now));
        db.Reports.Add(Report.Create(owner.Id, Guid.NewGuid(), owner.CityId, new GeoPoint(4.713, -74.074), "Solo para amigos.", false, false, TimeSpan.FromHours(2), Now, visibility: ContentVisibility.FriendsOnly));
        db.Reports.Add(Report.Create(outsider.Id, Guid.NewGuid(), outsider.CityId, new GeoPoint(4.714, -74.075), "De otra persona.", false, false, TimeSpan.FromHours(2), Now));
        var friendship = Accepted(owner.Id, friend.Id);
        db.Friendships.Add(friendship);
        await db.SaveChangesAsync();

        var profiles = new GetPublicProfileHandler(db, clock, new FakeLinks(), new Signer());
        var view = await profiles.Handle(new GetPublicProfileRequest(stranger.Id, "duena"), CancellationToken.None);
        Assert.Equal("Bio pública", view.Value!.Bio);
        Assert.Equal(owner.CreatedAt.Year, view.Value.MemberSinceYear);
        Assert.Equal(owner.CreatedAt.Month, view.Value.MemberSinceMonth);
        Assert.Equal("Reporte con nombre.", Assert.Single(view.Value.Reports).Excerpt);
        Assert.DoesNotContain("anónimo", view.Value.Reports[0].Excerpt, StringComparison.OrdinalIgnoreCase);

        var asFriend = await profiles.Handle(new GetPublicProfileRequest(friend.Id, "duena"), CancellationToken.None);
        Assert.Contains(asFriend.Value!.Reports, item => item.Excerpt == "Solo para amigos.");
        Assert.DoesNotContain(asFriend.Value.Reports, item => item.Excerpt.Contains("anónimo", StringComparison.OrdinalIgnoreCase));

        var reader = new ReportMapReader(db, clock);
        var map = new GetReportMapHandler(db, clock, reader, new Signer());
        var friendsOnly = await map.Handle(new GetReportMapRequest(friend.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, "friends", null, null), CancellationToken.None);
        Assert.Contains(friendsOnly.Value!.Items, item => item.IsMine == false && item.Author?.UserId == owner.Id);
        Assert.DoesNotContain(friendsOnly.Value.Items, item => item.Author?.UserId == outsider.Id);

        var anonymous = Report.Create(owner.Id, Guid.NewGuid(), owner.CityId, new GeoPoint(4.715, -74.076), "Anónimo de un amigo.", true, false, TimeSpan.FromHours(2), Now);
        var friendsAnonymous = Report.Create(owner.Id, Guid.NewGuid(), owner.CityId, new GeoPoint(4.716, -74.077), "Anónimo solo para amigos.", true, false, TimeSpan.FromHours(2), Now, visibility: ContentVisibility.FriendsOnly);
        db.Reports.AddRange(anonymous, friendsAnonymous);
        await db.SaveChangesAsync();
        var hiddenAnonymous = await map.Handle(new GetReportMapRequest(friend.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, "friends", null, null), CancellationToken.None);
        Assert.DoesNotContain(hiddenAnonymous.Value!.Items, item => item.Id == anonymous.Id || item.Id == friendsAnonymous.Id);
        var ownAnonymous = await map.Handle(new GetReportMapRequest(owner.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, "friends", null, null), CancellationToken.None);
        Assert.Contains(ownAnonymous.Value!.Items, item => item.Id == anonymous.Id && item.IsMine && item.IsAnonymous);
    }

    [Fact]
    public async Task A_friend_request_notifies_unless_the_recipient_turned_that_type_off()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adulta");
        var other = await UserAsync(db, clock, 28, "other@situ.test", "otro");
        var muted = await UserAsync(db, clock, 27, "muted@situ.test", "callado");
        db.NotificationPreferences.Add(NotificationPreference.Create(muted.Id, NotificationType.FriendRequest, NotificationChannel.InApp, false));
        await db.SaveChangesAsync();
        var send = new SendFriendRequestHandler(db, clock);

        await send.Handle(new SendFriendRequestRequest(adult.Id, other.Id), CancellationToken.None);
        await send.Handle(new SendFriendRequestRequest(adult.Id, muted.Id), CancellationToken.None);
        Assert.Contains(db.Notifications, item => item.RecipientId == other.Id && item.Type == NotificationType.FriendRequest && !item.Body.Contains('@') == false);
        Assert.DoesNotContain(db.Notifications, item => item.RecipientId == muted.Id);
        Assert.DoesNotContain(db.Notifications, item => item.Body.Contains("situ.test", StringComparison.OrdinalIgnoreCase));
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

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Situ.Api.Live;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction.Complaints;
using Situ.Application.Features.Live;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Domain.Moderation;
using Situ.Domain.Social;

namespace Situ.Application.Tests.Live;

public class LiveFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 15, 0, 0, TimeSpan.Zero);
    private const string Ingest = "ingest-secret-for-tests-32-characters";
    private const string Hls = "hls-secret-for-tests-32-characters-ok";

    [Fact]
    public void A_public_path_matches_the_mediamtx_opaque_route()
    {
        var path = LiveTokens.NewPath();
        Assert.Matches("^[a-f0-9]{32}$", path);
        Assert.DoesNotMatch("^[a-f0-9]{32}$", "poc");
        Assert.DoesNotMatch("^[a-f0-9]{32}$", path.ToUpperInvariant());
    }

    [Fact]
    public void Tokens_reject_an_expired_or_tampered_value_and_a_ttl_over_180_seconds()
    {
        var path = "abc123";
        var token = LiveTokens.SignHls(Hls, path, Now);
        Assert.True(LiveTokens.VerifyHls(Hls, path, token, Now.AddSeconds(30)));
        Assert.Equal("live.hls_expired", LiveTokens.HlsRefusal(Hls, path, token, Now.AddSeconds(121)));
        Assert.Equal("live.hls_bad_signature", LiveTokens.HlsRefusal(Hls, path, token + "aa", Now));
        Assert.Equal("live.hls_path_mismatch", LiveTokens.HlsRefusal(Hls, "other", token, Now));
        Assert.Equal("live.hls_missing_token", LiveTokens.HlsRefusal(Hls, path, "  ", Now));
        Assert.Equal("live.hls_path_mismatch", LiveTokens.HlsRefusal(Hls, null, token, Now));
        Assert.Throws<InvalidOperationException>(() => LiveTokens.SignHls(Hls, path, Now, 181));
        Assert.Equal("abc123", LiveTokens.PathFromUri("/hls/abc123/index.m3u8?token=1"));
        Assert.Equal(token, LiveTokens.TokenFromUri($"/hls/abc123/index.m3u8?token={token}"));
        Assert.Equal(token, LiveTokens.TokenFromUri($"/hls/abc123/{token}/seg.m4s"));
        Assert.Equal($"https://live.somossitu.com/hls/{path}/{token}/index.m3u8", LiveTokens.PlaybackUrl(new LiveStreamOptions { Domain = "somossitu.com" }, path, token));
    }

    [Fact]
    public void Loopback_and_the_ingest_secret_are_both_required()
    {
        Assert.True(LiveIngress.IsLoopback(System.Net.IPAddress.Loopback));
        Assert.True(LiveIngress.IsLoopback(System.Net.IPAddress.IPv6Loopback));
        Assert.False(LiveIngress.IsLoopback(System.Net.IPAddress.Parse("203.0.113.8")));
        Assert.False(LiveIngress.IsLoopback(null));
        Assert.True(LiveIngress.SecretMatches(Ingest, Ingest));
        Assert.False(LiveIngress.SecretMatches(Ingest, Hls));
        Assert.False(LiveIngress.SecretMatches(Ingest, ""));
    }

    [Fact]
    public void A_public_point_is_rounded_and_never_stored_as_received()
    {
        var raw = new GeoPoint(4.71101, -74.07211);
        var rounded = LiveLocation.Round(raw, 300);
        Assert.NotEqual(raw, rounded);
        Assert.True(raw.DistanceInMetersTo(rounded) <= 300);
        var friends = LiveLocation.Round(raw, 25);
        Assert.True(raw.DistanceInMetersTo(friends) <= 25);
    }

    [Fact]
    public async Task Create_enforces_age_one_stream_and_the_global_cap_and_hides_the_owner()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await PersonAsync(db, clock, 30, "adult@situ.test", "adulta");
        var second = await PersonAsync(db, clock, 28, "two@situ.test", "dos");
        var third = await PersonAsync(db, clock, 27, "three@situ.test", "tres");
        var minor = await PersonAsync(db, clock, 15, "minor@situ.test", "menor");
        var create = new CreateLiveHandler(db, clock, Options());

        var minorResult = await create.Handle(new CreateLiveRequest(minor.Id, "Friends", "Cerca", null, null, null), CancellationToken.None);
        Assert.True(minorResult.IsFailure);
        Assert.Equal("live.minors_cannot_stream", minorResult.Error!.Code);

        var first = await create.Handle(new CreateLiveRequest(adult.Id, "Public", "Retén", null, 4.71101, -74.07211), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.DoesNotContain(Ingest, first.Value!.PublishUrl, StringComparison.Ordinal);
        Assert.DoesNotContain(Hls, first.Value.PublishKey, StringComparison.Ordinal);
        Assert.Contains($"/{first.Value.Id}?user=situ&pass={first.Value.PublishKey}", first.Value.PublishUrl, StringComparison.Ordinal);
        var stored = await db.LiveStreams.SingleAsync(item => item.PublicPath == first.Value.Id);
        Assert.NotEqual(first.Value.PublishKey, stored.PublishKeyHash);
        Assert.Null(stored.IngestKey);
        Assert.True(stored.IsAnonymous);
        Assert.NotEqual(4.71101, stored.Location!.Value.Latitude);

        var again = await create.Handle(new CreateLiveRequest(adult.Id, "Public", "Otra", null, null, null), CancellationToken.None);
        Assert.Equal("live.already_live", again.Error!.Code);

        Assert.True((await create.Handle(new CreateLiveRequest(second.Id, "Friends", "Amigos", null, null, null), CancellationToken.None)).IsSuccess);
        var capped = await create.Handle(new CreateLiveRequest(third.Id, "Public", "Tercera", null, null, null), CancellationToken.None);
        Assert.Equal("live.limit_concurrent", capped.Error!.Code);

        var list = await new ListLiveHandler(db, clock).Handle(new ListLiveRequest(adult.Id), CancellationToken.None);
        Assert.Empty(list.Value!.Streams);
        Assert.DoesNotContain(typeof(LiveCard).GetProperties(), property => property.Name.Contains("Streamer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Watch_rechecks_friendship_and_blocks_and_the_key_is_single_use()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await PersonAsync(db, clock, 30, "owner@situ.test", "duena");
        var friend = await PersonAsync(db, clock, 29, "friend@situ.test", "amiga");
        var stranger = await PersonAsync(db, clock, 31, "stranger@situ.test", "ajena");
        var media = new FakeMedia();
        var created = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(owner.Id, "Friends", "En la 80", null, null, null),
            CancellationToken.None);
        var path = created.Value!.Id;
        var authorize = new AuthorizeLiveHandler(db, clock, Options());

        Assert.Equal("live.unauthorized", (await authorize.Handle(new AuthorizeLiveRequest(false, Ingest, "publish", path, created.Value.PublishKey, null), CancellationToken.None)).Error!.Code);
        Assert.Equal("live.unauthorized", (await authorize.Handle(new AuthorizeLiveRequest(true, "no", "publish", path, created.Value.PublishKey, null), CancellationToken.None)).Error!.Code);
        Assert.True((await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", path, created.Value.PublishKey, null), CancellationToken.None)).IsSuccess);
        Assert.Equal(LiveStreamStatus.Live, (await db.LiveStreams.SingleAsync(item => item.PublicPath == path)).Status);
        Assert.True((await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "read", path, null, null), CancellationToken.None)).IsSuccess);
        Assert.Equal("live.already_used", (await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", path, created.Value.PublishKey, null), CancellationToken.None)).Error!.Code);

        media.Paths.Add(new MediaMtxPath(path, true));
        var reconciled = await new ReconcileLiveHandler(db, clock, media).Handle(new ReconcileLiveRequest(), CancellationToken.None);
        Assert.Equal(0, reconciled.Value!.MarkedLive);

        var watch = new WatchLiveHandler(db, clock, Options());
        Assert.True((await watch.Handle(new WatchLiveRequest(stranger.Id, path), CancellationToken.None)).IsFailure);
        db.Friendships.Add(Accepted(owner.Id, friend.Id));
        await db.SaveChangesAsync();
        var allowed = await watch.Handle(new WatchLiveRequest(friend.Id, path), CancellationToken.None);
        Assert.True(allowed.IsSuccess);
        Assert.Contains($"/hls/{path}/", allowed.Value!.Url, StringComparison.Ordinal);
        Assert.EndsWith("/index.m3u8", allowed.Value.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("?", allowed.Value.Url, StringComparison.Ordinal);
        Assert.Equal("Situero anónimo", (await new GetLiveHandler(db, clock).Handle(new GetLiveRequest(friend.Id, path), CancellationToken.None)).Value!.DisplayName);

        db.UserBlocks.Add(UserBlock.CreateFull(friend.Id, owner.Id, Now));
        await db.SaveChangesAsync();
        Assert.True((await watch.Handle(new WatchLiveRequest(friend.Id, path), CancellationToken.None)).IsFailure);

        var end = await new EndLiveHandler(db, clock, media).Handle(new EndLiveRequest(owner.Id, path, false), CancellationToken.None);
        Assert.True(end.IsSuccess);
        Assert.Contains(path, media.Kicked);
        var ended = await db.LiveStreams.SingleAsync(item => item.PublicPath == path);
        Assert.Equal(LiveStreamStatus.Ended, ended.Status);
        Assert.Contains(ended.DomainEvents, item => item is LiveStreamEndedEvent { WasVisible: true });
        Assert.Equal("live.not_found", (await watch.Handle(new WatchLiveRequest(friend.Id, path), CancellationToken.None)).Error!.Code);
        Assert.True((await new GetLiveHandler(db, clock).Handle(new GetLiveRequest(friend.Id, path), CancellationToken.None)).IsFailure);
        db.UserBlocks.RemoveRange(await db.UserBlocks.ToListAsync());
        await db.SaveChangesAsync();
        var card = await new GetLiveHandler(db, clock).Handle(new GetLiveRequest(friend.Id, path), CancellationToken.None);
        Assert.Equal("Ended", card.Value!.Status);
        Assert.True((await new GetLiveHandler(db, clock).Handle(new GetLiveRequest(stranger.Id, path), CancellationToken.None)).IsFailure);
        Assert.True((await new GetLiveHandler(db, clock).Handle(new GetLiveRequest(friend.Id, "no-existe"), CancellationToken.None)).IsFailure);
    }

    [Fact]
    public async Task Reconcile_ends_a_live_stream_when_the_path_is_gone_or_the_time_limit_is_reached()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await PersonAsync(db, clock, 30, "drop@situ.test", "caida");
        var created = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(adult.Id, "Public", "Se cayó", null, null, null),
            CancellationToken.None);
        var path = created.Value!.Id;
        Assert.True((await new AuthorizeLiveHandler(db, clock, Options()).Handle(
            new AuthorizeLiveRequest(true, Ingest, "publish", path, created.Value.PublishKey, null),
            CancellationToken.None)).IsSuccess);

        var media = new FakeMedia();
        clock.UtcNow = Now.AddSeconds(20);
        await new ReconcileLiveHandler(db, clock, media).Handle(new ReconcileLiveRequest(), CancellationToken.None);
        var dropped = await db.LiveStreams.SingleAsync(item => item.PublicPath == path);
        Assert.Equal(LiveStreamStatus.Ended, dropped.Status);
        Assert.DoesNotContain(path, media.Kicked);
        Assert.Contains(dropped.DomainEvents, item => item is LiveStreamEndedEvent { WasVisible: true });

        var second = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(adult.Id, "Public", "Larga", null, null, null),
            CancellationToken.None);
        var longPath = second.Value!.Id;
        Assert.True((await new AuthorizeLiveHandler(db, clock, Options()).Handle(
            new AuthorizeLiveRequest(true, Ingest, "publish", longPath, second.Value.PublishKey, null),
            CancellationToken.None)).IsSuccess);
        media.Paths.Add(new MediaMtxPath(longPath, true));
        clock.UtcNow = clock.UtcNow.AddMinutes(30);
        await new ReconcileLiveHandler(db, clock, media).Handle(new ReconcileLiveRequest(), CancellationToken.None);
        var limited = await db.LiveStreams.SingleAsync(item => item.PublicPath == longPath);
        Assert.Equal(LiveStreamStatus.Ended, limited.Status);
        Assert.Contains(longPath, media.Kicked);
        Assert.Contains(limited.DomainEvents, item => item is LiveStreamEndedEvent { WasVisible: true });
    }

    [Fact]
    public async Task A_minor_friend_can_watch_another_minor_only_when_the_setting_allows_the_stream()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var older = await PersonAsync(db, clock, 17, "older@situ.test", "mayor");
        var younger = await PersonAsync(db, clock, 16, "younger@situ.test", "menor");
        var adult = await PersonAsync(db, clock, 30, "adult@situ.test", "adulta");
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == Situ.Domain.Administration.SystemSettingKeys.LiveMinorsCanStream);
        if (setting is null)
        {
            db.SystemSettings.Add(Situ.Domain.Administration.SystemSetting.Create(
                Situ.Domain.Administration.SystemSettingKeys.LiveMinorsCanStream,
                "true",
                null,
                Now));
        }
        else
        {
            setting.ChangeValue("true", Now);
        }

        await db.SaveChangesAsync();
        var created = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(older.Id, "Friends", "Colegio", null, null, null),
            CancellationToken.None);
        Assert.True(created.IsSuccess);
        var stream = await db.LiveStreams.SingleAsync();
        Assert.True(stream.AcceptPublishKey(stream.PublishKeyHash!, Now));
        Assert.True(stream.MarkLive(Now));
        await db.SaveChangesAsync();
        db.Friendships.Add(Accepted(older.Id, younger.Id));
        db.Friendships.Add(Accepted(older.Id, adult.Id));
        await db.SaveChangesAsync();

        var watch = new WatchLiveHandler(db, clock, Options());
        Assert.True((await watch.Handle(new WatchLiveRequest(younger.Id, created.Value!.Id), CancellationToken.None)).IsSuccess);
        Assert.True((await watch.Handle(new WatchLiveRequest(adult.Id, created.Value.Id), CancellationToken.None)).IsFailure);
    }

    [Fact]
    public async Task Authorize_names_why_a_publish_key_was_rejected_and_reads_the_query_when_password_is_empty()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await PersonAsync(db, clock, 30, "keys@situ.test", "claves");
        var created = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(adult.Id, "Public", "Clave", null, null, null),
            CancellationToken.None);
        var path = created.Value!.Id;
        var key = created.Value.PublishKey;
        var authorize = new AuthorizeLiveHandler(db, clock, Options());

        Assert.Equal("live.unknown_path", (await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", "0123456789abcdef0123456789abcdef", key, null), CancellationToken.None)).Error!.Code);
        Assert.Equal("live.missing_key", (await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", path, "  ", null), CancellationToken.None)).Error!.Code);
        Assert.Equal("live.invalid_key", (await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", path, "not-the-key", null), CancellationToken.None)).Error!.Code);

        clock.UtcNow = Now.AddMinutes(3);
        Assert.Equal("live.expired", (await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", path, key, null), CancellationToken.None)).Error!.Code);
        clock.UtcNow = Now;

        var stream = await db.LiveStreams.SingleAsync(item => item.PublicPath == path);
        stream.End("recording", Now, TimeSpan.Zero);
        await db.SaveChangesAsync();
        Assert.Equal("live.invalid_state", (await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", path, key, null), CancellationToken.None)).Error!.Code);
        stream.ExpireRecordingIfDue(Now);
        await db.SaveChangesAsync();
        Assert.Equal("live.limit", (await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", path, key, null), CancellationToken.None)).Error!.Code);

        var again = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(adult.Id, "Public", "Otra clave", null, null, null),
            CancellationToken.None);
        Assert.True((await authorize.Handle(new AuthorizeLiveRequest(true, Ingest, "publish", again.Value!.Id, null, $"user=situ&pass={Uri.EscapeDataString(again.Value.PublishKey)}"), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task A_waiting_stream_whose_key_expired_without_a_publisher_is_ended()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await PersonAsync(db, clock, 30, "zombie@situ.test", "zombie");
        var created = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(adult.Id, "Public", "Nadie", null, null, null),
            CancellationToken.None);
        clock.UtcNow = Now.AddMinutes(3);
        var media = new FakeMedia();
        await new ReconcileLiveHandler(db, clock, media).Handle(new ReconcileLiveRequest(), CancellationToken.None);
        var stream = await db.LiveStreams.SingleAsync(item => item.PublicPath == created.Value!.Id);
        Assert.Equal(LiveStreamStatus.Ended, stream.Status);
    }

    [Fact]
    public async Task Nginx_hls_headers_get_200_401_or_403_for_each_stream_state()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await PersonAsync(db, clock, 30, "hls@situ.test", "hls");
        var options = Options();
        var created = await new CreateLiveHandler(db, clock, options).Handle(
            new CreateLiveRequest(adult.Id, "Public", "En vivo", null, null, null),
            CancellationToken.None);
        var path = created.Value!.Id;
        var token = LiveTokens.SignHls(Hls, path, Now);
        var playlist = $"/hls/{path}/index.m3u8?token={Uri.EscapeDataString(token)}";
        var segment = $"/hls/{path}/{token}/seg.m4s";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ApplicationName = "Situ.Tests",
            ContentRootPath = Path.GetTempPath(),
            Args = [],
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IApplicationDbContext>(db);
        builder.Services.AddSingleton<IClock>(clock);
        builder.Services.AddSingleton<IOptions<LiveStreamOptions>>(options);
        builder.Services.AddSingleton<IHandler<AuthorizeLiveRequest, Result>, UnusedAuthorize>();
        builder.Services.AddSingleton<IHandler<CheckHlsTokenRequest, Result<CheckHlsTokenResponse>>>(
            new CheckHlsTokenHandler(db, clock, options));
        var app = builder.Build();
        LiveIngressEndpoints.Map(app);
        await app.StartAsync();
        try
        {
            Assert.Equal(403, await HlsStatus(app, playlist, header: ""));
            Assert.Equal(401, await HlsStatus(app, $"/hls/{path}/index.m3u8?token=no-es-token", header: ""));
            Assert.Equal(401, await HlsStatus(app, $"/hls/{path}/index.m3u8", header: ""));
            Assert.Equal(401, await HlsStatus(app, "/otro/index.m3u8?token=no-es-token", header: ""));

            Assert.True((await new AuthorizeLiveHandler(db, clock, options).Handle(
                new AuthorizeLiveRequest(true, Ingest, "publish", path, created.Value.PublishKey, null),
                CancellationToken.None)).IsSuccess);

            Assert.Equal(200, await HlsStatus(app, playlist, header: ""));
            Assert.Equal(200, await HlsStatus(app, segment, header: ""));
            Assert.Equal(200, await HlsStatus(app, playlist, header: token, path: "/internal/live/hls/"));
            Assert.Equal(200, await HlsStatus(app, playlist, header: token, path: "/internal/live/hls/extra"));
            Assert.NotEqual(404, await HlsStatus(app, playlist, header: "falso"));

            var other = LiveTokens.SignHls(Hls, new string('a', 32), Now);
            var missing = new string('c', 32);
            Assert.Equal(401, await HlsStatus(app, $"/hls/{path}/index.m3u8?token={other}", header: ""));
            Assert.Equal(401, await HlsStatus(app, $"/hls/{missing}/index.m3u8?token={LiveTokens.SignHls(Hls, missing, Now)}", header: ""));
            Assert.Equal(401, await HlsStatus(app, $"/hls/{new string('b', 32)}/index.m3u8?token={token}", header: ""));
            clock.UtcNow = Now.AddMinutes(3);
            Assert.Equal(401, await HlsStatus(app, playlist, header: ""));
            clock.UtcNow = Now;
            Assert.Equal(403, await HlsStatus(app, playlist, header: "", loopback: false));

            Assert.True((await new EndLiveHandler(db, clock, new FakeMedia()).Handle(
                new EndLiveRequest(adult.Id, path, false),
                CancellationToken.None)).IsSuccess);
            Assert.Equal(403, await HlsStatus(app, playlist, header: ""));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Reporting_a_live_stream_uses_the_complaint_type()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var owner = await PersonAsync(db, clock, 30, "owner2@situ.test", "dueno");
        var viewer = await PersonAsync(db, clock, 29, "viewer@situ.test", "mira");
        var created = await new CreateLiveHandler(db, clock, Options()).Handle(
            new CreateLiveRequest(owner.Id, "Public", "Público", null, null, null),
            CancellationToken.None);
        var stream = await db.LiveStreams.SingleAsync();
        Assert.True(stream.AcceptPublishKey(stream.PublishKeyHash!, Now));
        Assert.True(stream.MarkLive(Now));
        await db.SaveChangesAsync();
        var report = new ReportLiveHandler(db, clock, new FileComplaintHandler(db, clock));
        var filed = await report.Handle(new ReportLiveRequest(viewer.Id, created.Value!.Id, ComplaintReason.Spam, null), CancellationToken.None);
        Assert.True(filed.IsSuccess);
        Assert.Equal(ContentTargetType.LiveStream, (await db.Complaints.SingleAsync()).TargetType);
    }

    private sealed class UnusedAuthorize : IHandler<AuthorizeLiveRequest, Result>
    {
        public Task<Result> Handle(AuthorizeLiveRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private static async Task<int> HlsStatus(WebApplication app, string originalUri, string header, string path = "/internal/live/hls", bool loopback = true)
    {
        var body = new MemoryStream();
        var context = await app.GetTestServer().SendAsync(http =>
        {
            http.Connection.RemoteIpAddress = loopback ? IPAddress.Loopback : IPAddress.Parse("203.0.113.8");
            http.Request.Method = "GET";
            http.Request.Path = path;
            http.Request.Headers["X-Original-Uri"] = originalUri;
            http.Response.Body = body;
            if (header.Length > 0)
            {
                http.Request.Headers["X-Live-Token"] = header;
            }
        });

        var status = context.Response.StatusCode;
        if (status is not (200 or 401 or 403))
        {
            throw new InvalidOperationException($"HLS gate returned {status}.");
        }

        if (status == 200 && context.Response.Headers.ContainsKey("X-Live-Cookie"))
        {
            throw new InvalidOperationException("HLS gate set a cookie.");
        }

        return status;
    }

    private static IOptions<LiveStreamOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(new LiveStreamOptions
        {
            Domain = "somossitu.com",
            IngestSecret = Ingest,
            HlsSecret = Hls,
        });

    private static Friendship Accepted(Guid left, Guid right)
    {
        var friendship = Friendship.Request(left, right, Now);
        friendship.Accept(right, Now);
        return friendship;
    }

    private static async Task<User> PersonAsync(IdentityTestDb db, FixedClock clock, int years, string email, string userName)
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

    private sealed class FakeMedia : IMediaMtxControl
    {
        public List<MediaMtxPath> Paths { get; } = [];

        public List<string> Kicked { get; } = [];

        public Task<IReadOnlyList<MediaMtxPath>> ListPathsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaMtxPath>>(Paths);

        public Task<bool> KickPathAsync(string path, CancellationToken cancellationToken)
        {
            Kicked.Add(path);
            return Task.FromResult(true);
        }
    }
}

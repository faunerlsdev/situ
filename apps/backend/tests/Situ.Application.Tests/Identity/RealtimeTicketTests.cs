using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity.RealtimeTickets;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;

namespace Situ.Application.Tests.Identity;

public class RealtimeTicketTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_ticket_dies_when_reused_and_when_it_expires()
    {
        await using var db = IdentityTestDb.Create();
        var user = Person(db, "ana@correo.com", "ana");
        var device = UserDevice.Register(user.Id, "browser-1", DevicePlatform.Web, "Web", null, null, Now);
        db.UserDevices.Add(device);
        await db.SaveChangesAsync();
        var clock = new FixedClock(Now);
        var tokens = new PlainTokens();

        var issued = await new IssueRealtimeTicketHandler(db, clock, tokens).Handle(new IssueRealtimeTicketRequest(user.Id, device.Id, false), CancellationToken.None);
        Assert.True(issued.IsSuccess);
        var plain = issued.Value!.Ticket;
        Assert.Equal(Now.AddSeconds(30), issued.Value.ExpiresAt);

        var first = await new ConsumeRealtimeTicketHandler(db, clock, tokens).Handle(new ConsumeRealtimeTicketRequest(plain, null), CancellationToken.None);
        Assert.Equal(user.Id, first.Value!.UserId);
        Assert.Equal(device.Id, first.Value.DeviceId);

        var reused = await new ConsumeRealtimeTicketHandler(db, clock, tokens).Handle(new ConsumeRealtimeTicketRequest(plain, null), CancellationToken.None);
        Assert.Equal("realtime_ticket.used", reused.Error!.Code);

        var again = await new IssueRealtimeTicketHandler(db, clock, tokens).Handle(new IssueRealtimeTicketRequest(user.Id, device.Id, false), CancellationToken.None);
        clock.UtcNow = Now.AddSeconds(31);
        var expired = await new ConsumeRealtimeTicketHandler(db, clock, tokens).Handle(new ConsumeRealtimeTicketRequest(again.Value!.Ticket, null), CancellationToken.None);
        Assert.Equal("realtime_ticket.expired", expired.Error!.Code);
    }

    [Fact]
    public async Task A_ticket_for_one_user_is_rejected_for_another_and_is_not_consumed()
    {
        await using var db = IdentityTestDb.Create();
        var ana = Person(db, "ana@correo.com", "ana");
        var bruno = Person(db, "bruno@correo.com", "bruno");
        var device = UserDevice.Register(ana.Id, "browser-ana", DevicePlatform.Web, "Web", null, null, Now);
        var phone = UserDevice.Register(ana.Id, "phone-ana", DevicePlatform.Android, "Pixel", null, null, Now);
        db.UserDevices.AddRange(device, phone);
        await db.SaveChangesAsync();
        var clock = new FixedClock(Now);
        var tokens = new PlainTokens();

        var issued = await new IssueRealtimeTicketHandler(db, clock, tokens).Handle(new IssueRealtimeTicketRequest(ana.Id, device.Id, false), CancellationToken.None);
        var mismatch = await new ConsumeRealtimeTicketHandler(db, clock, tokens).Handle(new ConsumeRealtimeTicketRequest(issued.Value!.Ticket, bruno.Id), CancellationToken.None);
        Assert.Equal("realtime_ticket.user_mismatch", mismatch.Error!.Code);
        Assert.Null((await db.RealtimeTickets.SingleAsync()).ConsumedAt);

        var mobile = await new IssueRealtimeTicketHandler(db, clock, tokens).Handle(new IssueRealtimeTicketRequest(ana.Id, phone.Id, false), CancellationToken.None);
        Assert.Equal("realtime_ticket.device_required", mobile.Error!.Code);

        var staff = await new IssueRealtimeTicketHandler(db, clock, tokens).Handle(new IssueRealtimeTicketRequest(ana.Id, device.Id, true), CancellationToken.None);
        Assert.Equal("realtime_ticket.staff_forbidden", staff.Error!.Code);
    }

    private static User Person(IdentityTestDb db, string email, string userName)
    {
        var country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
        var region = Region.Create(country.Id, "Cundinamarca", "25");
        var city = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
        db.AddRange(country, region, city);
        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("300555" + (1000 + db.Users.Local.Count).ToString()),
            "hash",
            new DateOnly(1996, 4, 4),
            country.Id,
            region.Id,
            city.Id,
            "es-CO",
            14,
            Now);
        user.ConfirmEmail(Now);
        db.Users.Add(user);
        return user;
    }

    private sealed class PlainTokens : ITokenService
    {
        public string CreateAccessToken(Guid userId, DateTimeOffset expiresAt, string? scope = null, string? role = null, Guid? deviceId = null) => "access";

        public string CreateRefreshToken() => Guid.NewGuid().ToString("N");

        public string Hash(string plainToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken)));
    }
}

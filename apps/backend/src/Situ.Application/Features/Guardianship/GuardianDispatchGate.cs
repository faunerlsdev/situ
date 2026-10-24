using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Guardianship;

namespace Situ.Application.Features.Guardianship;

internal sealed record GuardianLimitRules(
    int MaxSendsPerMinorPerDay,
    int MinMinutesBetweenSends,
    int MaxEmailChangesPerDay,
    int MaxSendsPerEmailPerHour,
    int MaxSendsPerEmailPerDay,
    int MaxSendsPerIpPerHour,
    int MaxCodeAttempts,
    int MaxCodeResendsPer15Minutes)
{
    public static async Task<GuardianLimitRules> LoadAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.SystemSettings.AsNoTracking().ToListAsync(cancellationToken);
        return new GuardianLimitRules(
            Read(rows, SystemSettingKeys.GuardianMaxSendsPerMinorPerDay, 5),
            Read(rows, SystemSettingKeys.GuardianMinMinutesBetweenSends, 2),
            Read(rows, SystemSettingKeys.GuardianMaxEmailChangesPerDay, 3),
            Read(rows, SystemSettingKeys.GuardianMaxSendsPerEmailPerHour, 3),
            Read(rows, SystemSettingKeys.GuardianMaxSendsPerEmailPerDay, 10),
            Read(rows, SystemSettingKeys.GuardianMaxSendsPerIpPerHour, 10),
            Read(rows, SystemSettingKeys.GuardianMaxCodeAttempts, 5),
            Read(rows, SystemSettingKeys.GuardianMaxCodeResendsPer15Minutes, 3));
    }

    private static int Read(IReadOnlyList<SystemSetting> rows, string key, int fallback)
    {
        var raw = rows.FirstOrDefault(item => item.Key == key)?.Value;
        return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }
}

internal static class GuardianDispatchGate
{
    public static async Task<Error?> RejectionAsync(
        IApplicationDbContext db,
        Guid minorUserId,
        string destinationEmail,
        string? ipAddress,
        bool emailChange,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = destinationEmail.Trim().ToLowerInvariant();
        if (await db.GuardianEmailOptOuts.AnyAsync(item => item.Email == normalized, cancellationToken))
        {
            return GuardianErrors.GuardianEmailRejected();
        }

        var rules = await GuardianLimitRules.LoadAsync(db, cancellationToken);
        var dayAgo = now.AddHours(-24);
        var hourAgo = now.AddHours(-1);
        var mine = await db.GuardianConsentDispatches
            .Where(item => item.MinorUserId == minorUserId && item.SentAt >= dayAgo)
            .ToListAsync(cancellationToken);

        if (mine.Count > 0)
        {
            var last = mine.Max(item => item.SentAt);
            var next = last.AddMinutes(rules.MinMinutesBetweenSends);
            if (next > now)
            {
                return GuardianErrors.SendLimit(next);
            }
        }

        if (mine.Count >= rules.MaxSendsPerMinorPerDay)
        {
            return GuardianErrors.SendLimit(mine.Min(item => item.SentAt).AddHours(24));
        }

        if (emailChange && mine.Count(item => item.Kind == GuardianDispatchKind.EmailChange) >= rules.MaxEmailChangesPerDay)
        {
            var changes = mine.Where(item => item.Kind == GuardianDispatchKind.EmailChange).Select(item => item.SentAt);
            return GuardianErrors.SendLimit(changes.Min().AddHours(24));
        }

        var toEmail = await db.GuardianConsentDispatches
            .Where(item => item.DestinationEmail == normalized && item.SentAt >= dayAgo)
            .Select(item => item.SentAt)
            .ToListAsync(cancellationToken);
        var inHour = toEmail.Where(item => item >= hourAgo).ToList();
        if (inHour.Count >= rules.MaxSendsPerEmailPerHour)
        {
            return GuardianErrors.SendLimit(inHour.Min().AddHours(1));
        }

        if (toEmail.Count >= rules.MaxSendsPerEmailPerDay)
        {
            return GuardianErrors.SendLimit(toEmail.Min().AddHours(24));
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            var fromIp = await db.GuardianConsentDispatches
                .Where(item => item.IpAddress == ipAddress && item.SentAt >= hourAgo)
                .Select(item => item.SentAt)
                .ToListAsync(cancellationToken);
            if (fromIp.Count >= rules.MaxSendsPerIpPerHour)
            {
                return GuardianErrors.SendLimit(fromIp.Min().AddHours(1));
            }
        }

        return null;
    }

    public static async Task<DateTimeOffset?> NextRetryAsync(
        IApplicationDbContext db,
        Guid minorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rules = await GuardianLimitRules.LoadAsync(db, cancellationToken);
        var dayAgo = now.AddHours(-24);
        var mine = await db.GuardianConsentDispatches
            .Where(item => item.MinorUserId == minorUserId && item.SentAt >= dayAgo)
            .Select(item => item.SentAt)
            .ToListAsync(cancellationToken);
        if (mine.Count == 0)
        {
            return null;
        }

        var next = mine.Max().AddMinutes(rules.MinMinutesBetweenSends);
        if (mine.Count >= rules.MaxSendsPerMinorPerDay)
        {
            var daily = mine.Min().AddHours(24);
            if (daily > next)
            {
                next = daily;
            }
        }

        return next > now ? next : null;
    }
}

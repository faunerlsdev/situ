using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;

namespace Situ.Application.Features.Administration;

internal static class AdminSupport
{
    public static Error Unauthenticated() =>
        Error.Unauthorized("admin.unauthenticated", "Inicia sesión para usar el panel.");

    public static Error Forbidden() =>
        Error.Forbidden("admin.forbidden", "No tienes permiso para esta acción.");

    public static async Task<Error?> Allow(
        IApplicationDbContext db,
        Guid actorId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return Unauthenticated();
        }

        return await StaffGate.HasAsync(db, actorId, permission, cancellationToken) ? null : Forbidden();
    }

    public static void Record(
        IApplicationDbContext db,
        Guid? actorId,
        string action,
        string entityType,
        string? entityId,
        string? changes,
        string? ipAddress,
        DateTimeOffset now) =>
        db.AuditLogs.Add(AuditLog.Record(actorId, action, entityType, entityId, changes, ipAddress, now));

    public static string Json(object value) => JsonSerializer.Serialize(value);

    public static string MaskEmail(string email) => ProfileRules.MaskEmail(email);

    public static string? EncodeCursor(DateTimeOffset at, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(at.UtcTicks.ToString(CultureInfo.InvariantCulture) + ":" + id.ToString("N")));

    public static bool TryDecodeCursor(string? cursor, out DateTimeOffset at, out Guid id)
    {
        at = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var split = text.Split(':', 2);
            if (split.Length != 2 || !long.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return false;
            }

            at = new DateTimeOffset(ticks, TimeSpan.Zero);
            return Guid.TryParse(split[1], out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

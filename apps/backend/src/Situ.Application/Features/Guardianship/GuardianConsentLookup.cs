using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity;
using Situ.Domain.Guardianship;

namespace Situ.Application.Features.Guardianship;

internal static class GuardianConsentLookup
{
    public static async Task<GuardianConsent?> ByReviewTokenAsync(
        IApplicationDbContext db,
        string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = SecurityTokenCodec.Hash(token.Trim());
        return await db.GuardianConsents.FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
    }
}

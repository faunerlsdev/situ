using Microsoft.EntityFrameworkCore;
using Situ.Application.Features.Legal;
using Situ.Application.Tests.Support;
using Situ.Domain.Legal;

namespace Situ.Application.Tests.Legal;

public class TermsV11SwitchTests
{
    [Fact]
    public async Task Activate_makes_v11_current_and_revert_restores_the_previous_version()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var draft = LegalDocument.Publish(
            LegalDocumentType.TermsOfUse,
            catalog.CountryId,
            "es-CO",
            TermsV11Switch.NextVersion,
            "https://somossitu.com/terminos",
            RegistrationCatalog.Now);
        draft.Retire();
        db.LegalDocuments.Add(draft);
        await db.SaveChangesAsync();

        var activated = await TermsV11Switch.ActivateAsync(db, CancellationToken.None);
        Assert.True(activated.IsSuccess);
        var current = ApplicableLegalDocuments.SelectCurrent(await db.LegalDocuments.Where(item => item.IsActive).ToListAsync(), catalog.CountryId);
        Assert.Equal(TermsV11Switch.NextVersion, current.Single(item => item.Type == LegalDocumentType.TermsOfUse).Version);

        var reverted = await TermsV11Switch.RevertAsync(db, CancellationToken.None);
        Assert.True(reverted.IsSuccess);
        var restored = ApplicableLegalDocuments.SelectCurrent(await db.LegalDocuments.Where(item => item.IsActive).ToListAsync(), catalog.CountryId);
        Assert.Equal(TermsV11Switch.PreviousVersion, restored.Single(item => item.Type == LegalDocumentType.TermsOfUse).Version);
        Assert.False(draft.IsActive);
    }
}

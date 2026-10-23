using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>Localized name and description of a category (RF-GLO-03).</summary>
public sealed class ReportCategoryTranslation : AuditableEntity
{
    private ReportCategoryTranslation()
    {
        Locale = null!;
        Name = null!;
        Description = null!;
    }

    public Guid CategoryId { get; private set; }
    public string Locale { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }

    public static ReportCategoryTranslation Create(Guid categoryId, string locale, string name, string description) => new()
    {
        CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId)),
        Locale = Guard.NotEmpty(locale, nameof(locale), 15),
        Name = Guard.NotEmpty(name, nameof(name), 60),
        Description = Guard.NotEmpty(description, nameof(description), 160),
    };

    public void Update(string name, string description)
    {
        Name = Guard.NotEmpty(name, nameof(name), 60);
        Description = Guard.NotEmpty(description, nameof(description), 160);
    }
}

using Situ.Domain.Common;

namespace Situ.Domain.Profiles;

/// <summary>Predefined icon users can pick as avatar; managed from the admin panel (RF-PER-01).</summary>
public sealed class AvatarGalleryItem : AuditableEntity
{
    private AvatarGalleryItem()
    {
        Name = null!;
        ImageUrl = null!;
    }

    public string Name { get; private set; }
    public string ImageUrl { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static AvatarGalleryItem Create(string name, string imageUrl, int sortOrder) => new()
    {
        Name = Guard.NotEmpty(name, nameof(name), 60),
        ImageUrl = Guard.NotEmpty(imageUrl, nameof(imageUrl), 500),
        SortOrder = sortOrder,
        IsActive = true,
    };

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

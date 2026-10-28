using Situ.Application.Features.Administration.Catalog;
using Xunit;

namespace Situ.Application.Tests.Administration;

public sealed class CategoryIconFilesTests
{
    [Fact]
    public void Maps_seed_codes_onto_the_drawing_slugs()
    {
        Assert.Equal("accidente", CategoryIconFiles.SlugFor("accidente_transito"));
        Assert.Equal("protesta", CategoryIconFiles.SlugFor("manifestacion"));
        Assert.Equal("obra_via", CategoryIconFiles.SlugFor("obra_vial"));
        Assert.Equal("corte_luz", CategoryIconFiles.SlugFor("corte_servicio"));
        Assert.Equal("hurto", CategoryIconFiles.SlugFor("hurto"));
        Assert.Null(CategoryIconFiles.SlugFor("desconocido"));
        Assert.Equal(23, CategoryIconFiles.Slugs.Length);
        Assert.Equal("/assets/categories/button/accidente.png", CategoryIconFiles.ButtonPath("accidente"));
        Assert.Equal("/assets/categories/map/accidente.png", CategoryIconFiles.MapPath("accidente"));
    }
}

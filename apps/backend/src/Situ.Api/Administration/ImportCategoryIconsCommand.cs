using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Administration.Catalog;

namespace Situ.Api.Administration;

public static class ImportCategoryIconsCommand
{
    public static async Task<int> ExecuteAsync(WebApplication app, string[] args)
    {
        var source = args.Length > 1 ? args[1] : null;
        if (string.IsNullOrWhiteSpace(source) || (!File.Exists(source) && !Directory.Exists(source)))
        {
            Console.Error.WriteLine("Uso: categories:import-icons <zip-o-carpeta>");
            return 1;
        }

        var temp = Path.Combine(Path.GetTempPath(), "situ-category-icons-" + Guid.NewGuid().ToString("N"));
        try
        {
            var root = Directory.Exists(source) ? source : temp;
            if (!Directory.Exists(source))
            {
                Directory.CreateDirectory(temp);
                ZipFile.ExtractToDirectory(source, temp);
            }

            var pair = FindPair(root);
            if (pair is null)
            {
                Console.Error.WriteLine("El paquete no tiene png/button y png/map con accidente.png.");
                return 1;
            }

            var missing = CategoryIconFiles.Slugs.Where(slug => !File.Exists(Path.Combine(pair.Value.Button, slug + ".png")) || !File.Exists(Path.Combine(pair.Value.Map, slug + ".png"))).ToArray();
            if (missing.Length > 0)
            {
                Console.Error.WriteLine("Faltan iconos: " + string.Join(", ", missing));
                return 1;
            }

            var webRoot = app.Environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
            }

            CopySet(pair.Value.Button, Path.Combine(webRoot, "assets", "categories", "button"));
            CopySet(pair.Value.Map, Path.Combine(webRoot, "assets", "categories", "map"));

            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;
            var categories = await db.ReportCategories.ToListAsync();
            var updated = 0;
            foreach (var category in categories)
            {
                var slug = CategoryIconFiles.SlugFor(category.Code);
                if (slug is null)
                {
                    Console.WriteLine($"Sin dibujo para {category.Code}.");
                    continue;
                }

                category.ReplaceIcons(CategoryIconFiles.ButtonPath(slug), CategoryIconFiles.MapPath(slug), now);
                updated++;
            }

            await db.SaveChangesAsync();
            Console.WriteLine($"Iconos copiados. Categorías actualizadas: {updated}.");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"No se pudieron escribir los iconos ({exception.GetType().Name}). wwwroot debe poder escribirse.");
            return 1;
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, true);
            }
        }
    }

    private static (string Button, string Map)? FindPair(string root)
    {
        foreach (var button in Directory.EnumerateDirectories(root, "button", SearchOption.AllDirectories))
        {
            var map = Path.Combine(Directory.GetParent(button)!.FullName, "map");
            if (File.Exists(Path.Combine(button, "accidente.png")) && Directory.Exists(map))
            {
                return (button, map);
            }
        }

        return null;
    }

    private static void CopySet(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var slug in CategoryIconFiles.Slugs)
        {
            File.Copy(Path.Combine(from, slug + ".png"), Path.Combine(to, slug + ".png"), overwrite: true);
        }
    }
}

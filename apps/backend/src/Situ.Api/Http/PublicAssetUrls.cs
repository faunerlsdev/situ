namespace Situ.Api.Http;

public static class PublicAssetUrls
{
    public const string DefaultCategoryIcon = "/assets/categories/default.png";

    public static string Absolute(string baseUrl, string? path)
    {
        var value = string.IsNullOrWhiteSpace(path) ? DefaultCategoryIcon : path.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var root = baseUrl.Trim().TrimEnd('/');
        return value.StartsWith('/') ? root + value : $"{root}/{value}";
    }
}

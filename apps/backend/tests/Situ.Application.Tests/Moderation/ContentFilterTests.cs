using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Situ.Application.Common.Results;
using Situ.Application.Features.Moderation;
using Situ.Api.Http;

namespace Situ.Application.Tests.Moderation;

public sealed class ContentFilterTests
{
    private static readonly IReadOnlySet<string> Terms = ContentFilterCatalog.Parse("""
        puta
        porn
        te voy a matar
        """);

    [Theory]
    [InlineData("qué puta cosa", "term")]
    [InlineData("mira este porn", "term")]
    [InlineData("te voy a matar", "term")]
    [InlineData("llama al 300 123 4567", "phone")]
    [InlineData("cédula 1.118.531.823", "national_id")]
    [InlineData("documento 12345678", "phone")]
    [InlineData("vive en la calle 26", "address")]
    public void Rejects_blocked_text(string text, string reason)
    {
        Assert.Equal(reason, ContentFilter.Reason(text, Terms));
    }

    [Theory]
    [InlineData("hubo un accidente en el norte")]
    [InlineData("el semáforo no funciona")]
    [InlineData("año 2024")]
    public void Allows_an_ordinary_report(string text)
    {
        Assert.Null(ContentFilter.Reason(text, Terms));
    }

    [Fact]
    public void Rereads_the_word_file_when_it_changes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"situ-filter-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "zorrillo\n");
        try
        {
            Assert.Equal("term", ContentFilter.Reason("un zorrillo", ContentFilterCatalog.Read(path)));
            File.WriteAllText(path, "otrotermino\n");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
            Assert.Null(ContentFilter.Reason("un zorrillo", ContentFilterCatalog.Read(path)));
            Assert.Equal("term", ContentFilter.Reason("otrotermino aquí", ContentFilterCatalog.Read(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Field_error_is_422_with_field_errors()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Response.Body = new MemoryStream();
        var result = Result.Failure(ContentFilter.Rejected("description")).ToHttpResult();
        await result.ExecuteAsync(http);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        var body = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Assert.Contains("description", body, StringComparison.Ordinal);
        Assert.Contains(ContentFilter.Message, body, StringComparison.Ordinal);
        Assert.Contains("content.not_allowed", body, StringComparison.Ordinal);
    }
}

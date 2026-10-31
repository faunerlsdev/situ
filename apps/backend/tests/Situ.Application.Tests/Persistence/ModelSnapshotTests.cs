using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Situ.Infrastructure.Persistence;

namespace Situ.Application.Tests.Persistence;

public class ModelSnapshotTests
{
    [Fact]
    public void Snapshot_matches_the_current_model()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Database=situ_model_check;Username=situ;Password=situ",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;
        using var db = new AppDbContext(options);
        var migrations = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order().ToArray();
        Assert.Contains("20261030194500_OptionalRegistrationPlace", migrations);
        Assert.Contains("20261030210000_InactiveTermsV11", migrations);
        var script = db.GetService<IMigrator>().GenerateScript(
            "20261028230000_UpdateLegalPublication",
            "20261030210000_InactiveTermsV11");
        Assert.Contains("city_id", script);
        Assert.Contains("7ad39d3f-9848-4e1a-d5b4-d37731f778ba", script);

        var pending = db.Database.HasPendingModelChanges();
        if (!pending)
        {
            return;
        }

        var differ = db.GetService<IMigrationsModelDiffer>();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model.GetRelationalModel();
        var current = db.Model.GetRelationalModel();
        var diffs = differ.GetDifferences(snapshot, current);
        Assert.Fail(string.Join("\n", diffs.Select(diff => diff.ToString())));
    }
}

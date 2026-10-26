using System.Globalization;
using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>
/// Fixed geographic grid. The id is only the cell, never a user's coordinate.
/// The same function is implemented in packages/shared so the phone subscribes to the cells the server publishes.
/// </summary>
public static class MapCell
{
    public static string Id(double latitude, double longitude, double degrees)
    {
        GuardDegrees(degrees);
        return Format(Index(longitude, degrees), Index(latitude, degrees));
    }

    public static bool IsValid(string? cellId, double degrees)
    {
        if (degrees <= 0 || degrees > 1 || string.IsNullOrEmpty(cellId))
        {
            return false;
        }

        var split = cellId.IndexOf(':');
        if (split <= 0 || split != cellId.LastIndexOf(':'))
        {
            return false;
        }

        if (!int.TryParse(cellId[..split], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(cellId[(split + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            return false;
        }

        if (!string.Equals(Format(x, y), cellId, StringComparison.Ordinal))
        {
            return false;
        }

        return x >= Index(-180, degrees) && x <= Index(180, degrees)
            && y >= Index(-90, degrees) && y <= Index(90, degrees);
    }

    /// <summary>
    /// Cells touched by an axis-aligned viewport. A span that crosses the antimeridian, or more than
    /// <paramref name="maxCells"/> cells, is refused so the caller stays on the ordinary map load.
    /// </summary>
    public static bool TryCover(
        double minLatitude,
        double minLongitude,
        double maxLatitude,
        double maxLongitude,
        double degrees,
        int maxCells,
        out IReadOnlyList<string> cells)
    {
        cells = [];
        if (degrees <= 0 || degrees > 1 || maxCells < 1)
        {
            return false;
        }

        if (minLatitude > maxLatitude || minLongitude > maxLongitude)
        {
            return false;
        }

        if (minLatitude < -90 || maxLatitude > 90 || minLongitude < -180 || maxLongitude > 180)
        {
            return false;
        }

        var x0 = Index(minLongitude, degrees);
        var x1 = Index(maxLongitude, degrees);
        var y0 = Index(minLatitude, degrees);
        var y1 = Index(maxLatitude, degrees);
        var count = (long)(x1 - x0 + 1) * (y1 - y0 + 1);
        if (count <= 0 || count > maxCells)
        {
            return false;
        }

        var list = new List<string>((int)count);
        for (var x = x0; x <= x1; x++)
        {
            for (var y = y0; y <= y1; y++)
            {
                list.Add(Format(x, y));
            }
        }

        cells = list;
        return true;
    }

    private static int Index(double value, double degrees) => (int)Math.Floor(value / degrees);

    private static string Format(int x, int y) =>
        string.Create(CultureInfo.InvariantCulture, $"{x}:{y}");

    private static void GuardDegrees(double degrees)
    {
        if (degrees <= 0 || degrees > 1)
        {
            throw new DomainException("map.invalid_cell", "Cell size must be between 0 and 1 degree.");
        }
    }
}

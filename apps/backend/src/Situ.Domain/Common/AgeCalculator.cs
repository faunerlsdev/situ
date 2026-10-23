namespace Situ.Domain.Common;

public static class AgeCalculator
{
    /// <summary>Completed years between <paramref name="dateOfBirth"/> and <paramref name="on"/>.</summary>
    public static int AgeOn(DateOnly dateOfBirth, DateOnly on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (on < dateOfBirth.AddYears(age))
        {
            age--;
        }

        return age;
    }

    public static DateOnly ToDate(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);
}

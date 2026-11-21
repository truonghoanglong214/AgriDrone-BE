namespace AgriDrone.Modules.Surveys.Application.Features.Reports;

public sealed record FinancialReportPeriod(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public static FinancialReportPeriod Create(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9998);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.FromHours(7));
        return new(start.ToUniversalTime(), start.AddMonths(1).ToUniversalTime());
    }
    public bool Contains(DateTimeOffset? timestamp) => timestamp >= StartUtc && timestamp < EndUtc;
}

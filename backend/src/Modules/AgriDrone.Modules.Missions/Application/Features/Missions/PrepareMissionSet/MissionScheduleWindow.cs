namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

public sealed record MissionScheduleWindow(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt)
{
    private static readonly TimeZoneInfo OperationsTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static MissionScheduleWindow ForDate(DateOnly date)
    {
        var localStart = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(
            date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new MissionScheduleWindow(
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, OperationsTimeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, OperationsTimeZone)));
    }

    public static DateOnly DateAtOperationsLocation(DateTimeOffset timestamp) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            timestamp, OperationsTimeZone).DateTime);

    public bool Overlaps(DateTimeOffset startAt, DateTimeOffset endAt) =>
        StartAt < endAt && EndAt > startAt;

    public MissionScheduleWindow? Intersect(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        var start = StartAt > startAt ? StartAt : startAt;
        var end = EndAt < endAt ? EndAt : endAt;
        return start < end ? new MissionScheduleWindow(start, end) : null;
    }
}

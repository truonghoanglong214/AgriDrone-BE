namespace AgriDrone.Modules.Surveys.Domain;

/// <summary>
/// Hỗ trợ chuẩn hóa và chuyển đổi khung giờ cuộc hẹn khảo sát theo ngày (Date-based scheduling)
/// sử dụng múi giờ vận hành Asia/Ho_Chi_Minh.
/// </summary>
public static class SurveyAppointmentWindow
{
    public static readonly TimeZoneInfo OperationsTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    /// <summary>
    /// Chuẩn hóa một ngày cụ thể (ScheduledDate) thành khung thời gian UTC (ProposedStartAt, ProposedEndAt).
    /// </summary>
    public static bool TryResolveForDate(
        DateOnly date,
        DateTimeOffset nowUtc,
        out DateTimeOffset startUtc,
        out DateTimeOffset endUtc,
        out string? errorMessage)
    {
        startUtc = default;
        endUtc = default;
        errorMessage = null;

        var localStart = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(
            date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        var proposedStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, OperationsTimeZone));
        var proposedEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, OperationsTimeZone));

        if (proposedEnd <= nowUtc)
        {
            errorMessage = "The scheduled date has already ended.";
            return false;
        }

        if (proposedStart <= nowUtc)
        {
            var adjustedStart = nowUtc.AddMinutes(5);
            if (adjustedStart >= proposedEnd)
            {
                errorMessage = "The scheduled date window has expired.";
                return false;
            }

            proposedStart = adjustedStart;
        }

        startUtc = proposedStart;
        endUtc = proposedEnd;
        return true;
    }

    /// <summary>
    /// Chuẩn hóa một khoảng ngày (ScheduledStartDate đến ScheduledEndDate) thành khung thời gian UTC.
    /// </summary>
    public static bool TryResolveForDateRange(
        DateOnly startDate,
        DateOnly endDate,
        DateTimeOffset nowUtc,
        out DateTimeOffset startUtc,
        out DateTimeOffset endUtc,
        out string? errorMessage)
    {
        startUtc = default;
        endUtc = default;
        errorMessage = null;

        if (endDate < startDate)
        {
            errorMessage = "ScheduledEndDate must be greater than or equal to ScheduledStartDate.";
            return false;
        }

        var localStart = DateTime.SpecifyKind(
            startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(
            endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        var proposedStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, OperationsTimeZone));
        var proposedEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, OperationsTimeZone));

        if (proposedEnd <= nowUtc)
        {
            errorMessage = "The scheduled date range has already ended.";
            return false;
        }

        if (proposedStart <= nowUtc)
        {
            var adjustedStart = nowUtc.AddMinutes(5);
            if (adjustedStart >= proposedEnd)
            {
                errorMessage = "The scheduled date range window has expired.";
                return false;
            }

            proposedStart = adjustedStart;
        }

        startUtc = proposedStart;
        endUtc = proposedEnd;
        return true;
    }

    /// <summary>
    /// Chuyển đổi khung giờ UTC ngược lại thành ngày theo múi giờ vận hành Asia/Ho_Chi_Minh.
    /// </summary>
    public static (DateOnly? SingleDate, DateOnly? StartDate, DateOnly? EndDate) ToLocalDates(
        DateTimeOffset startAt,
        DateTimeOffset endAt)
    {
        if (startAt == default || endAt == default || endAt <= startAt)
        {
            return (null, null, null);
        }

        var localStart = TimeZoneInfo.ConvertTime(startAt, OperationsTimeZone);
        var localEndInclusive = TimeZoneInfo.ConvertTime(endAt.AddSeconds(-1), OperationsTimeZone);

        var startDate = DateOnly.FromDateTime(localStart.DateTime);
        var endDate = DateOnly.FromDateTime(localEndInclusive.DateTime);

        if (startDate == endDate)
        {
            return (startDate, startDate, endDate);
        }

        return (null, startDate, endDate);
    }
}

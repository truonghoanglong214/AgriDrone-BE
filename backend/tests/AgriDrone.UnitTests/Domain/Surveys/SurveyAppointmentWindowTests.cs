using System;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class SurveyAppointmentWindowTests
{
    private static readonly TimeZoneInfo VnTz =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    [Fact]
    public void TryResolveForDateWithFutureDateShouldReturnFullDayWindow()
    {
        // Arrange: Today is 2026-11-03 10:00:00 VN time (03:00:00 UTC)
        var nowUtc = new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero);
        var futureDate = new DateOnly(2026, 11, 10);

        // Act
        var success = SurveyAppointmentWindow.TryResolveForDate(
            futureDate,
            nowUtc,
            out var startUtc,
            out var endUtc,
            out var errorMessage);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // 2026-11-10 00:00:00 +07:00 is 2026-11-09 17:00:00 UTC
        Assert.Equal(TimeSpan.Zero, startUtc.Offset);
        Assert.Equal(TimeSpan.Zero, endUtc.Offset);

        var localStart = TimeZoneInfo.ConvertTime(startUtc, VnTz);
        var localEnd = TimeZoneInfo.ConvertTime(endUtc, VnTz);

        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0), localStart.DateTime);
        Assert.Equal(new DateTime(2026, 11, 11, 0, 0, 0), localEnd.DateTime);

        // Reverse conversion
        var (singleDate, startDate, endDate) = SurveyAppointmentWindow.ToLocalDates(startUtc, endUtc);
        Assert.Equal(futureDate, singleDate);
        Assert.Equal(futureDate, startDate);
        Assert.Equal(futureDate, endDate);
    }

    [Fact]
    public void TryResolveForDateWithTodayShouldStartAtRequestTime()
    {
        // Arrange: Now is 2026-11-03 11:30:00 VN time (04:30:00 UTC)
        var nowUtc = new DateTimeOffset(2026, 11, 3, 4, 30, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 11, 3);

        // Act
        var success = SurveyAppointmentWindow.TryResolveForDate(
            today,
            nowUtc,
            out var startUtc,
            out var endUtc,
            out var errorMessage);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        Assert.Equal(nowUtc, startUtc);
        Assert.True(endUtc > startUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 17, 0, 0, TimeSpan.Zero),
            endUtc);

        // Local date conversion should still correctly identify today
        var (singleDate, startDate, endDate) = SurveyAppointmentWindow.ToLocalDates(startUtc, endUtc);
        Assert.Equal(today, singleDate);
        Assert.Equal(today, startDate);
        Assert.Equal(today, endDate);
    }

    [Fact]
    public void TryResolveForDateWithPastDateShouldFail()
    {
        // Arrange: Now is 2026-11-03 11:30:00 VN time
        var nowUtc = new DateTimeOffset(2026, 11, 3, 4, 30, 0, TimeSpan.Zero);
        var pastDate = new DateOnly(2026, 11, 2);

        // Act
        var success = SurveyAppointmentWindow.TryResolveForDate(
            pastDate,
            nowUtc,
            out _,
            out _,
            out var errorMessage);

        // Assert
        Assert.False(success);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void TryResolveForDateRangeWithFutureRangeShouldSpanAllDays()
    {
        // Arrange
        var nowUtc = new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero);
        var startDate = new DateOnly(2026, 11, 10);
        var endDate = new DateOnly(2026, 11, 12);

        // Act
        var success = SurveyAppointmentWindow.TryResolveForDateRange(
            startDate,
            endDate,
            nowUtc,
            out var startUtc,
            out var endUtc,
            out var errorMessage);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        var localStart = TimeZoneInfo.ConvertTime(startUtc, VnTz);
        var localEnd = TimeZoneInfo.ConvertTime(endUtc, VnTz);

        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0), localStart.DateTime);
        Assert.Equal(new DateTime(2026, 11, 13, 0, 0, 0), localEnd.DateTime);

        var (singleDate, sDate, eDate) = SurveyAppointmentWindow.ToLocalDates(startUtc, endUtc);
        Assert.Null(singleDate);
        Assert.Equal(startDate, sDate);
        Assert.Equal(endDate, eDate);
    }

    [Fact]
    public void TryResolveForDateRangeWithEndBeforeStartShouldFail()
    {
        var nowUtc = new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero);
        var startDate = new DateOnly(2026, 11, 12);
        var endDate = new DateOnly(2026, 11, 10);

        var success = SurveyAppointmentWindow.TryResolveForDateRange(
            startDate,
            endDate,
            nowUtc,
            out _,
            out _,
            out var errorMessage);

        Assert.False(success);
        Assert.Contains("ScheduledEndDate must be greater than or equal to ScheduledStartDate", errorMessage);
    }
}

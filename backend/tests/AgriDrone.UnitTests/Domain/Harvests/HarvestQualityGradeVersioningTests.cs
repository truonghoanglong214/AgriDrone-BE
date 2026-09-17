using AgriDrone.Modules.Harvests.Domain.Quality;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Harvests;

public sealed class HarvestQualityGradeVersioningTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateInitializesFirstActiveRevision()
    {
        var grade = CreateGrade();

        Assert.NotEqual(Guid.Empty, grade.Id);
        Assert.Equal("PREMIUM", grade.Code);
        Assert.Equal("Premium", grade.Name);
        Assert.Equal(1, grade.DisplayOrder);
        Assert.Equal(1, grade.RevisionNumber);
        Assert.Null(grade.SupersedesId);
        Assert.True(grade.IsActive);
        Assert.Null(grade.RetiredAt);
        Assert.Equal(CreatedAt, grade.CreatedAt);
        Assert.Equal(CreatedAt, grade.UpdatedAt);
        Assert.Equal(1, grade.Version);
    }

    [Fact]
    public void CreateNextVersionRetiresCurrentAndCreatesActiveRevision()
    {
        var current = CreateGrade();
        var versionedAt = CreatedAt.AddHours(1);

        var next = current.CreateNextVersion(
            "Premium fruit",
            displayOrder: 2,
            versionedAt);

        Assert.False(current.IsActive);
        Assert.Equal(versionedAt, current.RetiredAt);
        Assert.Equal(2, current.Version);

        Assert.Equal(current.Code, next.Code);
        Assert.Equal(current.Id, next.SupersedesId);
        Assert.Equal(2, next.RevisionNumber);
        Assert.Equal("Premium fruit", next.Name);
        Assert.Equal(2, next.DisplayOrder);
        Assert.True(next.IsActive);
        Assert.Equal(1, next.Version);
    }

    [Fact]
    public void CreateAndVersionRejectNonUtcTimestamp()
    {
        var nonUtcTimestamp =
            new DateTimeOffset(2026, 9, 16, 15, 0, 0, TimeSpan.FromHours(7));

        Assert.Throws<ArgumentException>(() =>
            HarvestQualityGrade.Create(
                "PREMIUM",
                "Premium",
                1,
                nonUtcTimestamp));

        var grade = CreateGrade();

        Assert.Throws<ArgumentException>(() =>
            grade.CreateNextVersion(
                "Premium fruit",
                2,
                nonUtcTimestamp));
    }

    private static HarvestQualityGrade CreateGrade() =>
        HarvestQualityGrade.Create(
            " premium ",
            " Premium ",
            1,
            CreatedAt);
}

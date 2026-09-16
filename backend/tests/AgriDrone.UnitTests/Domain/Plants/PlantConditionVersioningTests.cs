using AgriDrone.Modules.Plants.Domain.Conditions;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

public sealed class PlantConditionVersioningTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateInitializesFirstActiveRevision()
    {
        var condition = CreateCondition();

        Assert.NotEqual(Guid.Empty, condition.Id);
        Assert.Equal("SUNBURN", condition.Code);
        Assert.Equal("Sunburn", condition.Name);
        Assert.Null(condition.ScientificName);
        Assert.Equal(ConditionType.AbioticDamage, condition.ConditionType);
        Assert.Equal("Heat damage", condition.Description);
        Assert.Equal(1, condition.RevisionNumber);
        Assert.Null(condition.SupersedesId);
        Assert.True(condition.IsActive);
        Assert.Null(condition.RetiredAt);
        Assert.Equal(CreatedAt, condition.CreatedAt);
        Assert.Equal(CreatedAt, condition.UpdatedAt);
        Assert.Equal(1, condition.Version);
    }

    [Fact]
    public void CreateNextRevisionRetiresCurrentRevisionAndPreservesIdentityFields()
    {
        var current = CreateCondition();
        var nextCreatedAt = CreatedAt.AddHours(1);

        var next = current.CreateNextRevision(
            "Sunburn damage",
            scientificName: null,
            "Updated heat-damage description",
            nextCreatedAt);

        Assert.False(current.IsActive);
        Assert.Equal(nextCreatedAt, current.RetiredAt);
        Assert.Equal(nextCreatedAt, current.UpdatedAt);
        Assert.Equal(2, current.Version);

        Assert.NotEqual(current.Id, next.Id);
        Assert.Equal(current.Code, next.Code);
        Assert.Equal(current.ConditionType, next.ConditionType);
        Assert.Equal(current.Id, next.SupersedesId);
        Assert.Equal(2, next.RevisionNumber);
        Assert.True(next.IsActive);
        Assert.Null(next.RetiredAt);
        Assert.Equal(1, next.Version);
    }

    [Fact]
    public void RetireChangesStateAndVersionOnlyOnce()
    {
        var condition = CreateCondition();
        var retiredAt = CreatedAt.AddMinutes(30);

        Assert.True(condition.Retire(retiredAt));
        Assert.False(condition.IsActive);
        Assert.Equal(retiredAt, condition.RetiredAt);
        Assert.Equal(retiredAt, condition.UpdatedAt);
        Assert.Equal(2, condition.Version);

        Assert.False(condition.Retire(retiredAt.AddMinutes(1)));
        Assert.Equal(retiredAt, condition.RetiredAt);
        Assert.Equal(2, condition.Version);
    }

    [Fact]
    public void CreateNextRevisionRejectsRetiredCondition()
    {
        var condition = CreateCondition();
        condition.Retire(CreatedAt.AddMinutes(1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            condition.CreateNextRevision(
                "Sunburn damage",
                scientificName: null,
                description: null,
                CreatedAt.AddMinutes(2)));

        Assert.Equal(
            "Only an active plant condition can be versioned.",
            exception.Message);
    }

    [Fact]
    public void CreateRejectsNonUtcTimestamp()
    {
        var nonUtcTimestamp =
            new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.FromHours(7));

        Assert.Throws<ArgumentException>(() =>
            PlantCondition.Create(
                "SUNBURN",
                "Sunburn",
                scientificName: null,
                ConditionType.AbioticDamage,
                description: null,
                nonUtcTimestamp));
    }

    private static PlantCondition CreateCondition() =>
        PlantCondition.Create(
            " sunburn ",
            " Sunburn ",
            scientificName: null,
            ConditionType.AbioticDamage,
            " Heat damage ",
            CreatedAt);
}

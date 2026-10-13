using AgriDrone.Modules.Identity.Domain.SystemManagers;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Identity;

public sealed class FarmManagerAssignmentTests
{
    [Fact]
    public void EndingAssignmentPreservesHistoryActorAndReason()
    {
        var assignedAt = new DateTimeOffset(
            2026, 9, 23, 4, 0, 0, TimeSpan.Zero);
        var assignedBy = Guid.NewGuid();
        var endedBy = Guid.NewGuid();
        var assignment = FarmManagerAssignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            assignedBy,
            "Initial onboarding assignment",
            assignedAt);

        assignment.End(
            endedBy,
            "Reassigned for regional coverage",
            assignedAt.AddDays(2),
            assignment.Version);

        Assert.False(assignment.IsActive);
        Assert.Equal(assignedBy, assignment.AssignedBy);
        Assert.Equal("Initial onboarding assignment", assignment.AssignmentReason);
        Assert.Equal(endedBy, assignment.EndedBy);
        Assert.Equal("Reassigned for regional coverage", assignment.EndReason);
        Assert.Equal(2, assignment.Version);
    }

    [Fact]
    public void EndingAssignmentRequiresReason()
    {
        var now = DateTimeOffset.UtcNow;
        var assignment = FarmManagerAssignment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Initial assignment",
            now);

        Assert.Throws<ArgumentException>(() => assignment.End(
            Guid.NewGuid(),
            " ",
            now,
            assignment.Version));
    }
}

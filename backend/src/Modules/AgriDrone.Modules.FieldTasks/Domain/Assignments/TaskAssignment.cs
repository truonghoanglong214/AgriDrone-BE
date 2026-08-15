using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.FieldTasks.Domain.Assignments;

public sealed class TaskAssignment : Entity
{
    private TaskAssignment()
    {
    }

    public Guid TaskId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid AssignedBy { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public DateTimeOffset? UnassignedAt { get; private set; }

    public FieldTask Task { get; private set; } = null!;
}

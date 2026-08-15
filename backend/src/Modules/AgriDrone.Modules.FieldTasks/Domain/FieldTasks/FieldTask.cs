using AgriDrone.Modules.FieldTasks.Domain.Assignments;
using AgriDrone.Modules.FieldTasks.Domain.Media;
using AgriDrone.Modules.FieldTasks.Domain.Updates;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.FieldTasks.Domain.FieldTasks;

public sealed class FieldTask : AggregateRoot
{
    private FieldTask()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid? PlantId { get; private set; }

    public Guid? SourceScanId { get; private set; }

    public FieldTaskType TaskType { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public FieldTaskPriority Priority { get; private set; }

    public FieldTaskStatus Status { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public uint Version { get; private set; }

    public ICollection<TaskAssignment> Assignments { get; private set; } = [];

    public ICollection<TaskMedia> Media { get; private set; } = [];

    public ICollection<TaskUpdate> Updates { get; private set; } = [];
}

using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.FieldTasks.Domain.Updates;

public sealed class TaskUpdate : Entity
{
    private TaskUpdate()
    {
    }

    public Guid TaskId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid UserId { get; private set; }

    public FieldTaskResult? Result { get; private set; }

    public string? Note { get; private set; }

    public Guid? CreatedScanId { get; private set; }

    public Guid? ClientOperationId { get; private set; }

    public DateTimeOffset? DeviceCreatedAt { get; private set; }

    public DateTimeOffset ServerReceivedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

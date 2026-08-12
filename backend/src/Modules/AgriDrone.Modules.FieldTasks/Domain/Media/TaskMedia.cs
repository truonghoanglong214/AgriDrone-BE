namespace AgriDrone.Modules.FieldTasks.Domain.Media;

public sealed class TaskMedia
{
    private TaskMedia()
    {
    }

    public Guid TaskId { get; private set; }

    public Guid MediaId { get; private set; }

    public Guid? UploadedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

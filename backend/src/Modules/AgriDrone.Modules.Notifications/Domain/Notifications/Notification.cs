using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Notifications.Domain.Notifications;

public sealed class Notification : Entity
{
    private Notification()
    {
    }

    public Guid UserId { get; private set; }

    public string NotificationType { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string Message { get; private set; } = null!;

    public string? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }
}

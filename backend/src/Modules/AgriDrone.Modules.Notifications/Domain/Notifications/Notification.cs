using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Notifications.Domain.Notifications;

public sealed class Notification : Entity
{
    private Notification()
    {
    }

    public Guid UserId { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? FarmId { get; private set; }

    public string NotificationType { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string Message { get; private set; } = null!;

    public string? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public Guid? SourcePublicationId { get; private set; }

    public static Notification ForPublishedPlantHealth(Guid ownerId, Guid tenantId, Guid farmId, Guid publicationId,
        string teamLabelLimitation, DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(ownerId); DomainGuard.NotEmpty(tenantId); DomainGuard.NotEmpty(farmId); DomainGuard.NotEmpty(publicationId);
        DomainGuard.Utc(createdAt); ArgumentException.ThrowIfNullOrWhiteSpace(teamLabelLimitation);
        return new()
        {
            Id = Guid.CreateVersion7(), UserId = ownerId, TenantId = tenantId, FarmId = farmId,
            NotificationType = "PLANT_HEALTH_PUBLISHED", Title = "Đã công bố kết quả khảo sát sơ bộ",
            Message = $"Kết quả sức khỏe cây trồng đã được duyệt và công bố. Đây là kết quả nghi ngờ sơ bộ, không phải chẩn đoán. {teamLabelLimitation}",
            EntityType = "PlantHealthPublication", EntityId = publicationId, SourcePublicationId = publicationId, CreatedAt = createdAt
        };
    }
}

using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Farms;

public sealed class Farm : AggregateRoot
{
    private Farm()
    {
    }

    private Farm(
        Guid id,
        Guid tenantId,
        string code,
        string name,
        string? address,
        Polygon? boundary,
        Point? centerPoint,
        decimal? areaHectares,
        GeneralStatus status,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        Address = address;
        Boundary = boundary;
        CenterPoint = centerPoint;
        AreaHectares = areaHectares;
        Status = status;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Address { get; private set; }

    public Polygon? Boundary { get; private set; }

    public Point? CenterPoint { get; private set; }

    public decimal? AreaHectares { get; private set; }

    public GeneralStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public long Version { get; private set; } = 1;

    public ICollection<FarmZone> Zones { get; private set; } = [];

    public static Farm Create(
        Guid tenantId,
        string code,
        string name,
        string? address,
        Polygon? boundary,
        Point? centerPoint,
        decimal? areaHectares,
        GeneralStatus status,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(createdAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (areaHectares < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(areaHectares));
        }

        return new Farm(
            Guid.NewGuid(),
            tenantId,
            code,
            name,
            address,
            boundary,
            centerPoint,
            areaHectares,
            status,
            createdBy,
            createdAt);
    }

    public void UpdateDetails(
        string name,
        string? address,
        Polygon? boundary,
        Point? centerPoint,
        decimal? areaHectares,
        DateTimeOffset updateAt)
    {
        DomainGuard.Utc(updateAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (areaHectares < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(areaHectares));
        }

        Address = address;
        Name = name;
        Boundary = boundary;
        CenterPoint = centerPoint;
        AreaHectares = areaHectares;
        UpdatedAt = updateAt;
        Version++;
    }

    public bool Activate(DateTimeOffset activatedAt)
    {
        DomainGuard.Utc(activatedAt);

        if (Status == GeneralStatus.Active && DeletedAt is null)
        {
            return false;
        }

        Status = GeneralStatus.Active;
        DeletedAt = null;
        UpdatedAt = activatedAt;
        Version++;

        return true;
    }

    public bool Archive(DateTimeOffset archivedAt)
    {
        DomainGuard.Utc(archivedAt);

        if (DeletedAt.HasValue)
        {
            return false;
        }

        if (archivedAt < CreatedAt)
        {
            throw new ArgumentException(
                "ArchivedAt cannot be earlier than CreatedAt.",
                nameof(archivedAt));
        }

        Status = GeneralStatus.Inactive;
        DeletedAt = archivedAt;
        UpdatedAt = archivedAt;
        Version++;

        return true;
    }

    public bool IsArchived => DeletedAt.HasValue;

    
}

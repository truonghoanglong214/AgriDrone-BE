using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Zones;

public sealed class FarmZone : Entity
{
    private FarmZone(
        Guid farmId,
        string code,
        string name,
        Polygon? boundary,
        decimal? areaHectares,
        GeneralStatus status,
        Guid createdBy,
        DateTimeOffset createdAt
        )
    {
        FarmId = farmId;
        Code = code;
        Name = name;
        Boundary = boundary;
        AreaHectares = areaHectares;
        Status = status;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid FarmId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public Polygon? Boundary { get; private set; }

    public decimal? AreaHectares { get; private set; }

    public GeneralStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public long Version { get; private set; } = 1;

    public Farm Farm { get; private set; } = null!;

    public ICollection<ZoneMapVersion> MapVersions { get; private set; } = [];

    public static FarmZone Create(
        string code,
        string name,
        Polygon? boundary,
        decimal? areaHectares,
        GeneralStatus status,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        return new FarmZone(
            Guid.NewGuid(),
            code,
            name,
            boundary,
            areaHectares,
            status,
            createdBy,
            createdAt);
    }

    public void UpdateDetails(string code,
        string name,
        Polygon? boundary,
        decimal? areaHectares,
        DateTimeOffset updateAt)
    {
        Code = code;
        Name = name;
        Boundary = boundary;
        AreaHectares = areaHectares;
        UpdatedAt = updateAt;
        Version++;
    }

}

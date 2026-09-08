using System;
using System.Collections.Generic;
using System.Text;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Zones
{
    internal interface IFarmZoneRepository
    {
        Task<FarmZone?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);

        Task<FarmZone?> GetByCodeAsync(
            Guid tenantId,
            Guid farmId,
            string code,
            CancellationToken cancellationToken = default);

        Task<bool> ActiveCodeExistsAsync(
            Guid tenantId,
            Guid farmId,
            string code,
            Guid? excludingZoneId = null,
            CancellationToken cancellationToken = default);

        Task<bool> ActiveBoundaryOverlapsAsync(
            Guid tenantId,
            Guid farmId,
            Polygon boundary,
            Guid? excludingZoneId = null,
            CancellationToken cancellationToken = default);

        void Add(FarmZone zone);
        void Update(FarmZone zone);
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Mapping
{
    public sealed record PlantMappingV1(
    Guid ObservationId,
    Guid PlantId,
    bool WasCreated);
}

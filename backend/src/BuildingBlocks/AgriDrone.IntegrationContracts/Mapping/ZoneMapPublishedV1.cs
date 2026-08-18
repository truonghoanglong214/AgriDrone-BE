using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Mapping
{
    public sealed record ZoneMapPublishedV1(
    Guid SourceMessageId,
    Guid ApprovalId,
    Guid MissionId,
    Guid FarmId,
    Guid ZoneId,
    Guid MapVersionId,
    int VersionNumber,
    DateTimeOffset PublishedAt,
    IReadOnlyList<PlantMappingV1> PlantMappings);
}

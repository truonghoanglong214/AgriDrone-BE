using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Mapping
{
    public sealed record MappingCandidateV1(
    Guid ObservationId,
    Guid? ResolvedPlantId,
    double Latitude,
    double Longitude,
    int RowIndex,
    int ColumnIndex,
    double? LocationAccuracyM,
    double PositionConfidence,
    string Decision);
}

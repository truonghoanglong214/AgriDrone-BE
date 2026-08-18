using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Mapping
{
    public sealed record MappingCandidatesApprovedV1(
    Guid ApprovalId,
    Guid MissionId,
    Guid FarmId,
    Guid ZoneId,
    Guid? ExpectedCurrentMapVersionId,
    string AlgorithmVersion,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<MappingCandidateV1> Candidates);
}

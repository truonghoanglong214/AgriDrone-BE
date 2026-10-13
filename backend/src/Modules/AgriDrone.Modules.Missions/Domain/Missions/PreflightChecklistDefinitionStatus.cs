using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public enum PreflightChecklistDefinitionStatus
{
    Draft,
    Active,
    Retired
}

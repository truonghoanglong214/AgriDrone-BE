using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition
{
    public sealed record CreatePlantConditionCommand(
        string Code,
        string Name,
        string? ScientificName,
        ConditionType ConditionType,
        string? Description,
        PlantHealthLabelKind? PlantHealthLabelKind = null,
        string? DatasetVersion = null,
        string? AnnotationRuleVersion = null,
        string? TeamLabelLimitation = null) : IRequest<Result<PlantConditionResponse>>;
}

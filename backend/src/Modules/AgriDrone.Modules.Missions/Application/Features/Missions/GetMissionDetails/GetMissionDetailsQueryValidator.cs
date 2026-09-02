using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.GetMissionDetails;

internal sealed class GetMissionDetailsQueryValidator
    : AbstractValidator<GetMissionDetailsQuery>
{
    public GetMissionDetailsQueryValidator()
    {
        RuleFor(query => query.TenantId)
            .NotEmpty();

        RuleFor(query => query.FarmId)
            .NotEmpty();

        RuleFor(query => query.MissionId)
            .NotEmpty();
    }
}

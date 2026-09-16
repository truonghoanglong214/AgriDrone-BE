using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDetails;

internal sealed class GetMissionMediaDetailsQueryValidator : AbstractValidator<GetMissionMediaDetailsQuery>
{
    public GetMissionMediaDetailsQueryValidator()
    {
        RuleFor(query => query.FarmId).NotEmpty();
        RuleFor(query => query.MissionId).NotEmpty();
        RuleFor(query => query.MediaId).NotEmpty();
    }
}

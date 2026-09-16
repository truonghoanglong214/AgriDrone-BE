using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDownloadUrl;

internal sealed class GetMissionMediaDownloadUrlQueryValidator : AbstractValidator<GetMissionMediaDownloadUrlQuery>
{
    public GetMissionMediaDownloadUrlQueryValidator()
    {
        RuleFor(query => query.FarmId).NotEmpty();
        RuleFor(query => query.MissionId).NotEmpty();
        RuleFor(query => query.MediaId).NotEmpty();
    }
}

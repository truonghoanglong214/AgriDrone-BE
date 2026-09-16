using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMedia;

internal sealed class GetMissionMediaQueryValidator : AbstractValidator<GetMissionMediaQuery>
{
    public GetMissionMediaQueryValidator()
    {
        RuleFor(query => query.FarmId).NotEmpty();
        RuleFor(query => query.MissionId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query).Must(query =>
                ((long)query.PageNumber - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("Pagination offset is too large.");
        RuleFor(query => query.MediaType).IsInEnum().When(query => query.MediaType.HasValue);
        RuleFor(query => query.MediaRole).IsInEnum().When(query => query.MediaRole.HasValue);
    }
}

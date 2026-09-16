using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;

internal sealed class GetMissionsQueryValidator : AbstractValidator<GetMissionsQuery>
{
    public GetMissionsQueryValidator()
    {
        RuleFor(query => query.FarmId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query).Must(query =>
                ((long)query.PageNumber - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("Pagination offset is too large.");
        RuleFor(query => query.ZoneId).Must(id => id != Guid.Empty)
            .WithMessage("ZoneId cannot be an empty GUID.");
        RuleFor(query => query.DroneId).Must(id => id != Guid.Empty)
            .WithMessage("DroneId cannot be an empty GUID.");
        RuleFor(query => query.MissionType).IsInEnum().When(query => query.MissionType.HasValue);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.Search).MaximumLength(100);
    }
}

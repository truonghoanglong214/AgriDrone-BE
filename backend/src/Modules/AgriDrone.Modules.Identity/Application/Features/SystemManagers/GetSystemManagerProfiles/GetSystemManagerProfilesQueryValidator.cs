using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfiles;

internal sealed class GetSystemManagerProfilesQueryValidator
    : AbstractValidator<GetSystemManagerProfilesQuery>
{
    public GetSystemManagerProfilesQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);
        RuleFor(query => query.Availability)
            .IsInEnum()
            .When(query => query.Availability.HasValue);
    }
}

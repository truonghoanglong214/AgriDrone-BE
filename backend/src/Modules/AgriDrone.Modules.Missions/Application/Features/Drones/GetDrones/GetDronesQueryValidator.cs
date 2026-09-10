using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDrones;

internal sealed class GetDronesQueryValidator
    : AbstractValidator<GetDronesQuery>
{
    public GetDronesQueryValidator()
    {
        RuleFor(query => query.TenantId)
            .NotEmpty();

        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);

        RuleFor(query => query.Search)
            .MaximumLength(100);
    }
}
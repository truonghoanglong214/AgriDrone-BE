using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetSystemDrones;

internal sealed class GetSystemDronesQueryValidator
    : AbstractValidator<GetSystemDronesQuery>
{
    public GetSystemDronesQueryValidator()
    {
        RuleFor(query => query.TenantId)
            .Must(tenantId =>
                tenantId.HasValue &&
                tenantId.Value != Guid.Empty)
            .When(query => query.TenantId.HasValue)
            .WithMessage(
                "TenantId must not be empty.");

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
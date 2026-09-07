using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;

internal sealed class AssignFarmMemberCommandValidator
    : AbstractValidator<AssignFarmMemberCommand>
{
    public AssignFarmMemberCommandValidator()
    {
        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.Role)
            .Equal(FarmMemberRole.Manager)
            .WithMessage(
                "Tenant Admin assignment currently supports only the MANAGER farm role.");

        RuleFor(command => command.AccessScope)
            .Equal(FarmAccessScope.AllZones)
            .WithMessage(
                "Tenant Admin assignment currently supports only the ALL_ZONES access scope.");

        RuleFor(command => command.ZoneIds)
            .NotNull()
            .Must(zoneIds => zoneIds.Count == 0)
            .WithMessage(
                "ZoneIds must be empty when AccessScope is ALL_ZONES.");

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}

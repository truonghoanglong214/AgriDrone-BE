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
            .IsInEnum();

        RuleFor(command => command.AccessScope)
            .IsInEnum();

        RuleFor(command => command.ZoneIds)
            .NotNull();

        RuleFor(command => command.ZoneIds)
            .Must(zoneIds => zoneIds is not null && zoneIds.Count == 0)
            .When(command =>
                command.AccessScope == FarmAccessScope.AllZones)
            .WithMessage(
                "ZoneIds must be empty when AccessScope is ALL_ZONES.");

        RuleFor(command => command.ZoneIds)
            .Must(zoneIds =>
                zoneIds is not null &&
                zoneIds.Count > 0 &&
                zoneIds.All(zoneId => zoneId != Guid.Empty) &&
                zoneIds.Distinct().Count() == zoneIds.Count)
            .When(command =>
                command.AccessScope == FarmAccessScope.SelectedZones)
            .WithMessage(
                "ZoneIds must contain distinct, non-empty zone IDs when AccessScope is SELECTED_ZONES.");

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}

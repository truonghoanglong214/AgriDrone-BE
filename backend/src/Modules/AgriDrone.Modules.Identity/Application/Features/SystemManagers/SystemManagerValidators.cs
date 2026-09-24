using FluentValidation;
using AgriDrone.Modules.Identity.Domain.SystemManagers;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal static class SystemManagerValidationRules
{
    public const int ReasonMaximumLength = 1000;

    public static IRuleBuilderOptions<T, string> ValidReason<T>(
        this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(ReasonMaximumLength);
}

internal sealed class CreateSystemManagerProfileCommandValidator
    : AbstractValidator<CreateSystemManagerProfileCommand>
{
    public CreateSystemManagerProfileCommandValidator() =>
        RuleFor(command => command.UserId).NotEmpty();
}

internal sealed class ActivateSystemManagerProfileCommandValidator
    : AbstractValidator<ActivateSystemManagerProfileCommand>
{
    public ActivateSystemManagerProfileCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

internal sealed class SuspendSystemManagerProfileCommandValidator
    : AbstractValidator<SuspendSystemManagerProfileCommand>
{
    public SuspendSystemManagerProfileCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

internal sealed class UpdateSystemManagerAvailabilityCommandValidator
    : AbstractValidator<UpdateSystemManagerAvailabilityCommand>
{
    public UpdateSystemManagerAvailabilityCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Availability).IsInEnum();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

internal sealed class UpdateSystemManagerQualificationCommandValidator
    : AbstractValidator<UpdateSystemManagerQualificationCommand>
{
    public UpdateSystemManagerQualificationCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command.ExpiresAt)
            .NotNull()
            .When(command => command.Status == FlightQualificationStatus.Qualified);
    }
}

internal sealed class AssignPrimaryFarmManagerCommandValidator
    : AbstractValidator<AssignPrimaryFarmManagerCommand>
{
    public AssignPrimaryFarmManagerCommandValidator()
    {
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.SystemManagerProfileId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedCurrentAssignmentVersion)
            .GreaterThan(0)
            .When(command => command.ExpectedCurrentAssignmentVersion.HasValue);
    }
}

internal sealed class EndPrimaryFarmManagerAssignmentCommandValidator
    : AbstractValidator<EndPrimaryFarmManagerAssignmentCommand>
{
    public EndPrimaryFarmManagerAssignmentCommandValidator()
    {
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}

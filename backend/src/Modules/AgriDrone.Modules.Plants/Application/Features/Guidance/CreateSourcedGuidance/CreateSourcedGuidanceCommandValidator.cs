using FluentValidation;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.CreateSourcedGuidance;

internal sealed class CreateSourcedGuidanceCommandValidator
    : AbstractValidator<CreateSourcedGuidanceCommand>
{
    public CreateSourcedGuidanceCommandValidator()
    {
        RuleFor(command => command.Code)
            .NotEmpty().MaximumLength(80)
            .Matches("^[A-Za-z][A-Za-z0-9_]*$");
        RuleFor(command => command.PlantConditionId).NotEmpty();
        RuleFor(command => command.SourceTitle).NotEmpty().MaximumLength(300);
        RuleFor(command => command.SourceReference).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.GuidanceText).NotEmpty().MaximumLength(4000);
        RuleFor(command => command)
            .Must(command => !command.EffectiveTo.HasValue ||
                !command.EffectiveFrom.HasValue ||
                command.EffectiveTo > command.EffectiveFrom)
            .WithMessage("EffectiveTo must be later than EffectiveFrom.");
    }
}

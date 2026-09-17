using FluentValidation;

namespace AgriDrone.Modules.Plants.Application.Features.RetirePlantCondition;

internal sealed class RetirePlantConditionCommandValidator
    : AbstractValidator<RetirePlantConditionCommand>
{
    public RetirePlantConditionCommandValidator()
    {
        RuleFor(command => command.ConditionId)
            .NotEmpty()
            .WithMessage("Plant condition id is required.");

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version must be greater than zero.");
    }
}

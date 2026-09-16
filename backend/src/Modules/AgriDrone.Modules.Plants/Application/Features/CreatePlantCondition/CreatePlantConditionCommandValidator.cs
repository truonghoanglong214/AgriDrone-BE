using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition
{
    internal sealed class CreatePlantConditionCommandValidator :AbstractValidator<CreatePlantConditionCommand>
    {
        public CreatePlantConditionCommandValidator()
        {
            RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Za-z][A-Za-z0-9_]*$")
            .WithMessage(
                "Condition code may contain letters, numbers and underscores.");

            RuleFor(command => command.Name)
                .NotEmpty()
                .MaximumLength(150);

            RuleFor(command => command.ScientificName)
                .MaximumLength(150);

            RuleFor(command => command.ConditionType)
                .IsInEnum();

            RuleFor(command => command.Description)
                .MaximumLength(2000);
        }
    }
}

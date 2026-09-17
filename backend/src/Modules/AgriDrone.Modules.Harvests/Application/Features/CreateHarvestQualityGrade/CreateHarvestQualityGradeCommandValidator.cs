using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade
{
    internal sealed class CreateHarvestQualityGradeCommandValidator : AbstractValidator<CreateHarvestQualityGradeCommand>
    {
        public CreateHarvestQualityGradeCommandValidator()
        {
            RuleFor(command => command.Code)
                .NotEmpty()
                .MaximumLength(30)
                .Matches("^[A-Za-z][A-Za-z0-9_]*$");

            RuleFor(command => command.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(command => command.DisplayOrder)
                .GreaterThanOrEqualTo(0);
        }
    }
}

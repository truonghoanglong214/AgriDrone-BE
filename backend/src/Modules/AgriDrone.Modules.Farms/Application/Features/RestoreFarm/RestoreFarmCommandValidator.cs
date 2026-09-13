using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.RestoreFarm
{
    internal sealed class RestoreFarmCommandValidator : AbstractValidator<RestoreFarmCommand>
    {
        public RestoreFarmCommandValidator()
        {
            RuleFor(x => x.FarmId)
                .NotEmpty().WithMessage("FarmId is required.");

            RuleFor(x => x.ExpectedVersion)
                .GreaterThan(0).WithMessage("Expected version must be greater than zero.");
        }
    }
}

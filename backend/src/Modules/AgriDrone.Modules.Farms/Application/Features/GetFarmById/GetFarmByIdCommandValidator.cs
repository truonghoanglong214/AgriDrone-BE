using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.GetFarmById
{
    internal sealed class GetFarmByIdCommandValidator : AbstractValidator<GetFarmByIdCommand>
    {
        public GetFarmByIdCommandValidator()
        {
            RuleFor(x => x.FarmId)
                .NotEqual(Guid.Empty)
                .WithMessage("Farm ID is required.");
        }
    }
}

using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Features.GetFarm
{
    internal sealed class GetFarmQueryValidator : AbstractValidator<GetFarmQuery>
    {
        public GetFarmQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                 .InclusiveBetween(1, int.MaxValue).WithMessage("Page number must be a positive integer.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
        }
    }
}

using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;

internal sealed class GetArchivedFarmsQueryValidator
    : AbstractValidator<GetArchivedFarmsQuery>
{
    public GetArchivedFarmsQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be a positive integer.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}

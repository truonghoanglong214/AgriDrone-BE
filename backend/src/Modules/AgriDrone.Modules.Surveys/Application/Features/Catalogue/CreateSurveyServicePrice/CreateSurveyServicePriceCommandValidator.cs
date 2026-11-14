using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServicePrice;

internal sealed class CreateSurveyServicePriceCommandValidator
    : AbstractValidator<CreateSurveyServicePriceCommand>
{
    public CreateSurveyServicePriceCommandValidator()
    {
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.AmountPerPole)
            .GreaterThanOrEqualTo(0.01m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3)
            .Must(currency => string.Equals(
                currency.Trim(),
                "VND",
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only VND per-pole prices are supported.");
        RuleFor(command => command.ExpectedServiceVersion).GreaterThan(0u);
        RuleFor(command => command).Must(command =>
            command.MappingPricePerPole.HasValue == command.PlantHealthPricePerPole.HasValue &&
            (!command.MappingPricePerPole.HasValue ||
             (command.MappingPricePerPole > 0m && command.PlantHealthPricePerPole > 0m &&
              command.MappingPricePerPole == decimal.Truncate(command.MappingPricePerPole.Value) &&
              command.PlantHealthPricePerPole == decimal.Truncate(command.PlantHealthPricePerPole!.Value) &&
              command.MappingPricePerPole + command.PlantHealthPricePerPole == command.AmountPerPole)))
            .WithMessage("Supply both positive whole-VND component rates with AmountPerPole equal to their sum.");
    }
}

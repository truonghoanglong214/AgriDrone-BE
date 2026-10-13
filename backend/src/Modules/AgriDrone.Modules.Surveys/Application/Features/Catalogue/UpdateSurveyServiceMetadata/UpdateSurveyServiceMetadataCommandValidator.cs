using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.UpdateSurveyServiceMetadata;

internal sealed class UpdateSurveyServiceMetadataCommandValidator
    : AbstractValidator<UpdateSurveyServiceMetadataCommand>
{
    public UpdateSurveyServiceMetadataCommandValidator()
    {
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
    }
}

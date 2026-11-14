using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServiceRevision;

internal sealed class PublishSurveyServiceRevisionCommandValidator
    : AbstractValidator<PublishSurveyServiceRevisionCommand>
{
    public PublishSurveyServiceRevisionCommandValidator()
    {
        RuleFor(x => x.SurveyServiceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.MapsPoles)
            .Equal(true)
            .WithMessage("MapsPoles capability must be enabled. Dragon fruit disease screening requires pole mapping.");
        RuleFor(x => x.ExpectedLatestVersionNumber)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedLatestVersionNumber.HasValue);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceReference).MaximumLength(500);
    }
}

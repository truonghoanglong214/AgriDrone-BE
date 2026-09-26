using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyServicePrice : Entity
{
    private SurveyServicePrice() { }

    public Guid SurveyServiceId { get; private set; }
    public decimal PricePerHa { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SurveyService SurveyService { get; private set; } = null!;
}

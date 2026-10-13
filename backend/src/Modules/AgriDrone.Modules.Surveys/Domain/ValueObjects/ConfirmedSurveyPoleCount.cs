namespace AgriDrone.Modules.Surveys.Domain;

public readonly record struct ConfirmedSurveyPoleCount
{
    private ConfirmedSurveyPoleCount(int value) => Value = value;

    public int Value { get; }

    public static ConfirmedSurveyPoleCount Create(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return new ConfirmedSurveyPoleCount(value);
    }
}

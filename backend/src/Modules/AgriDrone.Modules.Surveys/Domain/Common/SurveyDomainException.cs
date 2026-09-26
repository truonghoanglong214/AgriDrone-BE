namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyDomainException : InvalidOperationException
{
    public SurveyDomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}

using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;

namespace AgriDrone.Modules.Surveys.Infrastructure.Requests;

internal sealed class SurveyRequestNumberGenerator
    : ISurveyRequestNumberGenerator
{
    public string Create() => $"SR-{Guid.CreateVersion7():N}";
}

using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Requests;

internal static class SurveyRequestIdempotencyFactory
{
    public static RequestIdempotency ForTenantActor(
        Guid tenantId,
        Guid actorId,
        string key)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);

        return RequestIdempotency.Create(
            $"tenant:{tenantId:N}:actor:{actorId:N}",
            key);
    }
}

using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalFailureInjector
    : ISurveyApprovalFailureInjector
{
    public Task AfterFlushAsync(
        SurveyApprovalPersistenceCheckpoint checkpoint,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

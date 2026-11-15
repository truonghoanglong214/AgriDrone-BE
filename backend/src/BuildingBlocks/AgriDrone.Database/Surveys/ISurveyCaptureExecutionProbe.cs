namespace AgriDrone.Database.Surveys;

public enum SurveyCaptureCheckpoint
{
    BeforeOrderLock,
    AfterAttemptAndMissionTracked,
    AfterStateTransitionsTracked,
    AfterAuditAndOutboxTracked,
    AfterSaveBeforeCommit
}

public interface ISurveyCaptureExecutionProbe
{
    ValueTask HitAsync(
        SurveyCaptureCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}

public sealed class NoopSurveyCaptureExecutionProbe : ISurveyCaptureExecutionProbe
{
    public ValueTask HitAsync(
        SurveyCaptureCheckpoint checkpoint,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}

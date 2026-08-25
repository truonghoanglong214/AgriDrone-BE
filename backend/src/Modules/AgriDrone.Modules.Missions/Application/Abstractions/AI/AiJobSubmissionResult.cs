namespace AgriDrone.Modules.Missions.Application.Abstractions.AI;

public sealed record AiJobSubmissionResult(
    string ExternalJobId,
    string Status,
    DateTimeOffset AcceptedAt);
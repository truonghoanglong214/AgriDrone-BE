using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record DetectMissionDiseasesCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    double ConfidenceThreshold = 0.25,
    JsonElement? PolygonGeoJson = null,
    double? MatchingToleranceMeters = null,
    double? ZoneRadiusMeters = null,
    int? MinDiseasedTrees = null)
    : IRequest<Result<MissionDiseaseJobResponse>>;

public sealed record GetMissionDiseaseJobQuery(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    Guid? JobId) : IRequest<Result<MissionDiseaseJobResponse>>;

public sealed record MissionDiseaseJobResponse(
    Guid JobId,
    Guid MissionId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DetectMissionDiseasesResult? Result,
    string? Error);

internal sealed record MissionDiseaseInput(
    Guid ActorId,
    double ConfidenceThreshold,
    JsonElement? PolygonGeoJson,
    double? MatchingToleranceMeters,
    double? ZoneRadiusMeters,
    int? MinDiseasedTrees);

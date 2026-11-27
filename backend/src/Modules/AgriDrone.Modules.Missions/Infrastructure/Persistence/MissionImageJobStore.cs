using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Application.Features.Processing;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionImageJobStore(MissionsDbContext db)
{
    public const string Tiling = "TILING";
    public const string Yolo = "YOLO";

    internal sealed record Job(
        Guid Id, Guid MissionId, Guid TenantId, Guid FarmId, string Kind, string Status, string Input, string? Output,
        string? Error, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt)
    {
        public T ReadInput<T>() where T : class => JsonSerializer.Deserialize<T>(Input)!;

        public MissionYoloJobResponse ToYoloResponse() => new(
            Id, MissionId, ReadInput<MissionYoloInput>().TilingJobId, Status, CreatedAt, StartedAt, CompletedAt,
            Output is null ? null : JsonSerializer.Deserialize<DetectMissionTreesResult>(Output), Error);

        public MissionTilingJobResponse ToResponse() => new(
            Id, MissionId, Status, CreatedAt, StartedAt, CompletedAt, ReadInput<MissionTilingInput>().PolygonGeoJson,
            Output is null ? null : JsonSerializer.Deserialize<SliceTilesResult>(Output), Error);
    }

    private const string Projection = """
        SELECT id AS "Id", mission_id AS "MissionId", tenant_id AS "TenantId", farm_id AS "FarmId",
               kind AS "Kind", status AS "Status", input::text AS "Input", output::text AS "Output", error AS "Error",
               created_at AS "CreatedAt", started_at AS "StartedAt", completed_at AS "CompletedAt"
        FROM mission.tiling_jobs
        """;

    public Task<Job?> FindAsync(Guid tenantId, Guid farmId, Guid missionId, string kind, Guid? jobId, CancellationToken token) =>
        db.Database.SqlQueryRaw<Job>(Projection)
            .Where(job => job.TenantId == tenantId && job.FarmId == farmId && job.MissionId == missionId &&
                          job.Kind == kind && (jobId == null || job.Id == jobId))
            .OrderByDescending(job => job.CreatedAt).ThenByDescending(job => job.Id).FirstOrDefaultAsync(token);

    public Task<Job?> FindActiveAsync(Guid missionId, string kind, CancellationToken token) =>
        db.Database.SqlQueryRaw<Job>(Projection)
            .SingleOrDefaultAsync(job => job.MissionId == missionId && job.Kind == kind &&
                (job.Status == "QUEUED" || job.Status == "RUNNING"), token);

    public Task<bool> MatchesAsync(Guid id, string input, CancellationToken token) =>
        db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM mission.tiling_jobs WHERE id = {id} AND input = CAST({input} AS jsonb)) AS "Value"
            """).SingleAsync(token);

    public Task<int> InsertAsync(Guid id, Guid tenantId, Guid farmId, Guid missionId, string kind, string input,
        DateTimeOffset now, CancellationToken token) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO mission.tiling_jobs (id, tenant_id, farm_id, mission_id, kind, status, input, created_at)
            VALUES ({id}, {tenantId}, {farmId}, {missionId}, {kind}, 'QUEUED', CAST({input} AS jsonb), {now})
            """, token);

    public Task<List<Job>> ReadPendingAsync(CancellationToken token) =>
        db.Database.SqlQueryRaw<Job>(Projection)
            .Where(job => job.Status == "QUEUED" || job.Status == "RUNNING")
            .OrderBy(job => job.Status).ThenBy(job => job.CreatedAt).ThenBy(job => job.Id).ToListAsync(token);

    public async Task<bool> TryLockAsync(Guid id, CancellationToken token) =>
        (await db.Database.SqlQuery<bool>($"""
            SELECT pg_try_advisory_lock(hashtextextended(CAST({id} AS text), 7649)) AS "Value"
            """).ToListAsync(token)).Single();

    public Task<int> UnlockAsync(Guid id, CancellationToken token) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_advisory_unlock(hashtextextended(CAST({id} AS text), 7649))
            """, token);

    public Task<int> StartAsync(Guid id, Guid runId, DateTimeOffset now, CancellationToken token) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE mission.tiling_jobs SET status = 'RUNNING', run_id = {runId}, started_at = {now}
            WHERE id = {id} AND status IN ('QUEUED', 'RUNNING')
            """, token);

    public Task<int> FinishAsync(Guid id, Guid runId, string? output, string? error,
        DateTimeOffset now, CancellationToken token)
    {
        var status = output is null ? "FAILED" : "COMPLETED";
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE mission.tiling_jobs
            SET status = {status}, output = CAST({output} AS jsonb), error = {error}, completed_at = {now}
            WHERE id = {id} AND run_id = {runId} AND status = 'RUNNING'
            """, token);
    }
}

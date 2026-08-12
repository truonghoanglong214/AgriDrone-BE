using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.FieldTasks.Domain.Updates;
using AgriDrone.Modules.Harvests.Domain.Seasons;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Plants.Domain.Diseases;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database;

internal static class PostgreSqlEnumMappings
{
    public static void ConfigureModel(ModelBuilder modelBuilder)
    {
        var translator = UpperSnakeCaseNameTranslator.Instance;

        modelBuilder.HasPostgresEnum<UserStatus>(DbSchemas.System, "user_status", translator);
        modelBuilder.HasPostgresEnum<FarmMemberRole>(DbSchemas.System, "farm_member_role", translator);
        modelBuilder.HasPostgresEnum<GeneralStatus>(DbSchemas.System, "general_status", translator);
        modelBuilder.HasPostgresEnum<PlantLifecycleStatus>(DbSchemas.System, "plant_lifecycle_status", translator);
        modelBuilder.HasPostgresEnum<HealthStatus>(DbSchemas.System, "health_status", translator);
        modelBuilder.HasPostgresEnum<DroneStatus>(DbSchemas.System, "drone_status", translator);
        modelBuilder.HasPostgresEnum<MissionType>(DbSchemas.System, "mission_type", translator);
        modelBuilder.HasPostgresEnum<MissionStatus>(DbSchemas.System, "mission_status", translator);
        modelBuilder.HasPostgresEnum<ProcessingStatus>(DbSchemas.System, "processing_status", translator);
        modelBuilder.HasPostgresEnum<MediaType>(DbSchemas.System, "media_type", translator);
        modelBuilder.HasPostgresEnum<MissionMediaRole>(DbSchemas.System, "mission_media_role", translator);
        modelBuilder.HasPostgresEnum<AiModelType>(DbSchemas.System, "ai_model_type", translator);
        modelBuilder.HasPostgresEnum<AiJobType>(DbSchemas.System, "ai_job_type", translator);
        modelBuilder.HasPostgresEnum<AiJobStatus>(DbSchemas.System, "ai_job_status", translator);
        modelBuilder.HasPostgresEnum<ObservationReviewStatus>(DbSchemas.System, "observation_review_status", translator);
        modelBuilder.HasPostgresEnum<PlantChangeType>(DbSchemas.System, "plant_change_type", translator);
        modelBuilder.HasPostgresEnum<ReviewStatus>(DbSchemas.System, "review_status", translator);
        modelBuilder.HasPostgresEnum<ScanSource>(DbSchemas.System, "scan_source", translator);
        modelBuilder.HasPostgresEnum<ScanReviewStatus>(DbSchemas.System, "scan_review_status", translator);
        modelBuilder.HasPostgresEnum<ScanMediaRole>(DbSchemas.System, "scan_media_role", translator);
        modelBuilder.HasPostgresEnum<DiseaseSeverity>(DbSchemas.System, "disease_severity", translator);
        modelBuilder.HasPostgresEnum<FindingSource>(DbSchemas.System, "finding_source", translator);
        modelBuilder.HasPostgresEnum<VerificationDecision>(DbSchemas.System, "verification_decision", translator);
        modelBuilder.HasPostgresEnum<SeasonStatus>(DbSchemas.System, "season_status", translator);
        modelBuilder.HasPostgresEnum<FieldTaskType>(DbSchemas.System, "task_type", translator);
        modelBuilder.HasPostgresEnum<FieldTaskPriority>(DbSchemas.System, "task_priority", translator);
        modelBuilder.HasPostgresEnum<FieldTaskStatus>(DbSchemas.System, "task_status", translator);
        modelBuilder.HasPostgresEnum<FieldTaskResult>(DbSchemas.System, "task_result", translator);
    }

    public static void ConfigureDataSource(NpgsqlDataSourceBuilder dataSourceBuilder)
    {
        var translator = UpperSnakeCaseNameTranslator.Instance;

        dataSourceBuilder.MapEnum<UserStatus>("system.user_status", translator);
        dataSourceBuilder.MapEnum<FarmMemberRole>("system.farm_member_role", translator);
        dataSourceBuilder.MapEnum<GeneralStatus>("system.general_status", translator);
        dataSourceBuilder.MapEnum<PlantLifecycleStatus>("system.plant_lifecycle_status", translator);
        dataSourceBuilder.MapEnum<HealthStatus>("system.health_status", translator);
        dataSourceBuilder.MapEnum<DroneStatus>("system.drone_status", translator);
        dataSourceBuilder.MapEnum<MissionType>("system.mission_type", translator);
        dataSourceBuilder.MapEnum<MissionStatus>("system.mission_status", translator);
        dataSourceBuilder.MapEnum<ProcessingStatus>("system.processing_status", translator);
        dataSourceBuilder.MapEnum<MediaType>("system.media_type", translator);
        dataSourceBuilder.MapEnum<MissionMediaRole>("system.mission_media_role", translator);
        dataSourceBuilder.MapEnum<AiModelType>("system.ai_model_type", translator);
        dataSourceBuilder.MapEnum<AiJobType>("system.ai_job_type", translator);
        dataSourceBuilder.MapEnum<AiJobStatus>("system.ai_job_status", translator);
        dataSourceBuilder.MapEnum<ObservationReviewStatus>("system.observation_review_status", translator);
        dataSourceBuilder.MapEnum<PlantChangeType>("system.plant_change_type", translator);
        dataSourceBuilder.MapEnum<ReviewStatus>("system.review_status", translator);
        dataSourceBuilder.MapEnum<ScanSource>("system.scan_source", translator);
        dataSourceBuilder.MapEnum<ScanReviewStatus>("system.scan_review_status", translator);
        dataSourceBuilder.MapEnum<ScanMediaRole>("system.scan_media_role", translator);
        dataSourceBuilder.MapEnum<DiseaseSeverity>("system.disease_severity", translator);
        dataSourceBuilder.MapEnum<FindingSource>("system.finding_source", translator);
        dataSourceBuilder.MapEnum<VerificationDecision>("system.verification_decision", translator);
        dataSourceBuilder.MapEnum<SeasonStatus>("system.season_status", translator);
        dataSourceBuilder.MapEnum<FieldTaskType>("system.task_type", translator);
        dataSourceBuilder.MapEnum<FieldTaskPriority>("system.task_priority", translator);
        dataSourceBuilder.MapEnum<FieldTaskStatus>("system.task_status", translator);
        dataSourceBuilder.MapEnum<FieldTaskResult>("system.task_result", translator);
    }
}

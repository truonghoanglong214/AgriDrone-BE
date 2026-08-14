using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.FieldTasks.Domain.Assignments;
using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.FieldTasks.Domain.Media;
using AgriDrone.Modules.FieldTasks.Domain.Updates;
using AgriDrone.Modules.Harvests.Domain.HarvestBatches;
using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.Modules.Harvests.Domain.Seasons;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Domain.ZoneAssignments;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Notifications.Domain.Notifications;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.SharedInfrastructure.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

internal static class CrossModuleRelationshipConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureIdentityAndFarms(modelBuilder);
        ConfigureMissions(modelBuilder);
        ConfigurePlants(modelBuilder);
        ConfigureHarvests(modelBuilder);
        ConfigureFieldTasks(modelBuilder);
        ConfigureNotificationsAndAudit(modelBuilder);
    }

    private static void ConfigureIdentityAndFarms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Farm>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(farm => farm.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farms_tenants_tenant_id");

        modelBuilder.Entity<FarmMembership>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(membership => new
            {
                membership.FarmId,
                membership.TenantId
            })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_farm_memberships_farms_same_tenant");

        modelBuilder.Entity<ZoneAssignment>()
            .HasOne<FarmZone>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.ZoneId, assignment.FarmId })
            .HasPrincipalKey(zone => new { zone.Id, zone.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_zone_assignments_zones_same_farm");

        modelBuilder.Entity<Farm>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(farm => farm.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farms_users_created_by");

        modelBuilder.Entity<FarmZone>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(zone => zone.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_zones_users_created_by");
    }

    private static void ConfigureMissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ZoneMapVersion>()
            .HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(mapVersion => new { mapVersion.SourceMissionId, mapVersion.FarmId })
            .HasPrincipalKey(mission => new { mission.Id, mission.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_zone_map_versions_source_mission_same_farm");

        modelBuilder.Entity<ZoneMapVersion>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(mapVersion => mapVersion.ConfirmedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_zone_map_versions_users_confirmed_by");

        modelBuilder.Entity<Drone>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(drone => drone.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_drones_tenants_tenant_id");

        modelBuilder.Entity<DroneMission>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(mission => new { mission.FarmId, mission.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_drone_missions_farms_same_tenant");

        modelBuilder.Entity<DroneMission>()
            .HasOne<FarmZone>()
            .WithMany()
            .HasForeignKey(mission => new { mission.ZoneId, mission.FarmId })
            .HasPrincipalKey(zone => new { zone.Id, zone.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_drone_missions_zone_same_farm");

        modelBuilder.Entity<DroneMission>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(mission => mission.PilotUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_drone_missions_users_pilot_user_id");

        modelBuilder.Entity<DroneMission>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(mission => mission.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_drone_missions_users_created_by");

        modelBuilder.Entity<MediaAsset>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(media => media.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_media_assets_tenants_tenant_id");

        modelBuilder.Entity<MediaAsset>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(media => new { media.FarmId, media.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_media_assets_farms_same_tenant");

        modelBuilder.Entity<MediaAsset>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(media => media.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_media_assets_users_uploaded_by");

        modelBuilder.Entity<AiThresholdProfile>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(profile => profile.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ai_threshold_profiles_users_created_by");

        modelBuilder.Entity<AiDetectionThreshold>()
            .HasOne<PlantCondition>()
            .WithMany()
            .HasForeignKey(threshold => threshold.ConditionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ai_detection_thresholds_conditions_condition_id");

        modelBuilder.Entity<MissionPlantObservation>()
            .HasOne<Plant>()
            .WithMany()
            .HasForeignKey(observation => new
            {
                observation.SuggestedPlantId,
                observation.FarmId
            })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_observation_suggested_plant_same_farm");

        modelBuilder.Entity<MissionPlantObservation>()
            .HasOne<Plant>()
            .WithMany()
            .HasForeignKey(observation => new
            {
                observation.ResolvedPlantId,
                observation.FarmId
            })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_observation_resolved_plant_same_farm");

        modelBuilder.Entity<MissionPlantObservation>()
            .HasOne<ZoneMapVersion>()
            .WithMany()
            .HasForeignKey(observation => new { observation.MapVersionId, observation.FarmId })
            .HasPrincipalKey(mapVersion => new { mapVersion.Id, mapVersion.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_observations_map_versions_same_farm");

        modelBuilder.Entity<ObservationMatchCandidate>()
            .HasOne<Plant>()
            .WithMany()
            .HasForeignKey(candidate => new { candidate.PlantId, candidate.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_match_candidates_plants_same_farm");

        modelBuilder.Entity<MissionPlantObservation>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(observation => observation.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_observations_users_reviewed_by");
    }

    private static void ConfigurePlants(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plant>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(plant => plant.FarmId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_farms_farm_id");

        modelBuilder.Entity<Plant>()
            .HasOne<FarmZone>()
            .WithMany()
            .HasForeignKey(plant => new { plant.ZoneId, plant.FarmId })
            .HasPrincipalKey(zone => new { zone.Id, zone.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_zone_same_farm");

        modelBuilder.Entity<Plant>()
            .HasOne<ZoneMapVersion>()
            .WithMany()
            .HasForeignKey(plant => new
            {
                plant.CurrentMapVersionId,
                plant.ZoneId,
                plant.FarmId
            })
            .HasPrincipalKey(mapVersion => new
            {
                mapVersion.Id,
                mapVersion.ZoneId,
                mapVersion.FarmId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plants_current_map_version_same_zone");

        modelBuilder.Entity<Plant>()
            .HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(plant => plant.CreatedFromMissionId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_plants_missions_created_from_mission_id");

        modelBuilder.Entity<PlantChangeEvent>()
            .HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(changeEvent => new { changeEvent.MissionId, changeEvent.FarmId })
            .HasPrincipalKey(mission => new { mission.Id, mission.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_change_event_mission_same_farm");

        modelBuilder.Entity<PlantChangeEvent>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(changeEvent => changeEvent.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_change_events_users_created_by");

        modelBuilder.Entity<PlantChangeEvent>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(changeEvent => changeEvent.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_plant_change_events_users_reviewed_by");

        modelBuilder.Entity<PlantScan>()
            .HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(scan => new { scan.MissionId, scan.FarmId })
            .HasPrincipalKey(mission => new { mission.Id, mission.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_scan_mission_same_farm");

        modelBuilder.Entity<PlantScan>()
            .HasOne<AiProcessingJob>()
            .WithMany()
            .HasForeignKey(scan => scan.AiJobId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_plant_scans_ai_jobs_ai_job_id");

        modelBuilder.Entity<PlantScan>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(scan => scan.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_plant_scans_users_created_by");

        modelBuilder.Entity<PlantScan>()
            .HasOne<FieldTask>()
            .WithMany()
            .HasForeignKey(scan => new { scan.SourceTaskId, scan.FarmId })
            .HasPrincipalKey(fieldTask => new { fieldTask.Id, fieldTask.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_scans_source_task_same_farm");

        modelBuilder.Entity<PlantScanMedia>()
            .HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(media => media.MediaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_scan_media_media_assets_media_id");

        modelBuilder.Entity<ConditionDetection>()
            .HasOne<AiModelVersion>()
            .WithMany()
            .HasForeignKey(detection => detection.ModelVersionId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_condition_detections_ai_models_model_version_id");

        modelBuilder.Entity<ConditionDetection>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(detection => detection.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_condition_detections_users_created_by");

        modelBuilder.Entity<ConditionLesion>()
            .HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(lesion => lesion.MediaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_condition_lesions_media_assets_media_id");

        modelBuilder.Entity<ScanVerification>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(verification => verification.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_scan_verifications_users_user_id");
    }

    private static void ConfigureHarvests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Season>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(season => season.FarmId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_seasons_farms_farm_id");

        modelBuilder.Entity<HarvestBatch>()
            .HasOne<FarmZone>()
            .WithMany()
            .HasForeignKey(batch => new { batch.ZoneId, batch.FarmId })
            .HasPrincipalKey(zone => new { zone.Id, zone.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_harvest_batch_zone_same_farm");

        modelBuilder.Entity<HarvestBatch>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(batch => batch.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_harvest_batches_users_created_by");

        modelBuilder.Entity<HarvestBatch>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(batch => batch.CompletedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_harvest_batches_users_completed_by");

        modelBuilder.Entity<PlantHarvestRecord>()
            .HasOne<Plant>()
            .WithMany()
            .HasForeignKey(record => new { record.PlantId, record.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_harvest_plant_same_farm");

        modelBuilder.Entity<PlantHarvestRecord>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(record => record.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_harvest_records_users_recorded_by");
    }

    private static void ConfigureFieldTasks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FieldTask>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(fieldTask => fieldTask.FarmId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_field_tasks_farms_farm_id");

        modelBuilder.Entity<FieldTask>()
            .HasOne<Plant>()
            .WithMany()
            .HasForeignKey(fieldTask => new { fieldTask.PlantId, fieldTask.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_plant_same_farm");

        modelBuilder.Entity<FieldTask>()
            .HasOne<PlantScan>()
            .WithMany()
            .HasForeignKey(fieldTask => new { fieldTask.SourceScanId, fieldTask.FarmId })
            .HasPrincipalKey(scan => new { scan.Id, scan.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_scan_same_farm");

        modelBuilder.Entity<FieldTask>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(fieldTask => fieldTask.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_field_tasks_users_created_by");

        modelBuilder.Entity<TaskAssignment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_assignments_users_user_id");

        modelBuilder.Entity<TaskAssignment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_assignments_users_assigned_by");

        modelBuilder.Entity<TaskUpdate>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(update => update.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_updates_users_user_id");

        modelBuilder.Entity<TaskUpdate>()
            .HasOne<PlantScan>()
            .WithMany()
            .HasForeignKey(update => new { update.CreatedScanId, update.FarmId })
            .HasPrincipalKey(scan => new { scan.Id, scan.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_updates_created_scan_same_farm");

        modelBuilder.Entity<TaskMedia>()
            .HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(media => media.MediaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_task_media_media_assets_media_id");

        modelBuilder.Entity<TaskMedia>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(media => media.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_task_media_users_uploaded_by");
    }

    private static void ConfigureNotificationsAndAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_notifications_users_user_id");

        modelBuilder.Entity<Notification>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(notification => notification.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_tenants_tenant_id");

        modelBuilder.Entity<Notification>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(notification => new { notification.FarmId, notification.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_farms_same_tenant");

        modelBuilder.Entity<AuditLog>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(auditLog => auditLog.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_audit_logs_users_user_id");

        modelBuilder.Entity<AuditLog>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(auditLog => new { auditLog.FarmId, auditLog.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_farms_same_tenant");

        modelBuilder.Entity<AuditLog>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(auditLog => auditLog.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_tenants_tenant_id");

        modelBuilder.Entity<AuditLog>()
            .HasOne<AiProcessingJob>()
            .WithMany()
            .HasForeignKey(auditLog => auditLog.SourceJobId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_audit_logs_ai_jobs_source_job_id");
    }
}

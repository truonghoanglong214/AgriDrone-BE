using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
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
using AgriDrone.Modules.Surveys.Domain;
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
        ConfigureSurveys(modelBuilder);
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

        modelBuilder.Entity<FarmManagerAssignment>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.FarmId,
                assignment.TenantId
            })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_manager_assignments_farms_same_tenant");

        modelBuilder.Entity<FarmManagerAssignment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_manager_assignments_users_assigned_by");

        modelBuilder.Entity<FarmManagerAssignment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.EndedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_manager_assignments_users_ended_by");

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

    private static void ConfigureSurveys(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SurveyServicePrice>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(price => price.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_service_prices_users_created_by");

        modelBuilder.Entity<SurveyRequest>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(request => request.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_requests_tenants_tenant_id");

        modelBuilder.Entity<SurveyRequest>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(request => new { request.FarmId, request.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_requests_farms_same_tenant");

        modelBuilder.Entity<SurveyRequest>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(request => request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_requests_users_requested_by");

        modelBuilder.Entity<SurveyRequestReview>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(review => review.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_request_reviews_users_reviewed_by");

        modelBuilder.Entity<SurveyOrder>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(order => order.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_orders_tenants_tenant_id");

        modelBuilder.Entity<SurveyOrder>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(order => new { order.FarmId, order.TenantId })
            .HasPrincipalKey(farm => new { farm.Id, farm.TenantId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_orders_farms_same_tenant");

        modelBuilder.Entity<SurveyOrder>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(order => order.ScopeConfirmedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_orders_users_scope_confirmed_by");

        modelBuilder.Entity<SurveyAppointment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(appointment => appointment.ConfirmedByTenantOwnerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_appointments_users_confirmed_by");

        modelBuilder.Entity<PriceAdjustment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(adjustment => adjustment.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_price_adjustments_users_requested_by");

        modelBuilder.Entity<PriceAdjustment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(adjustment => adjustment.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_price_adjustments_users_approved_by");

        modelBuilder.Entity<SurveyResult>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(result => result.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_results_users_reviewed_by");

        modelBuilder.Entity<SurveyResult>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(result => result.PublishedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_results_users_published_by");

        modelBuilder.Entity<HarvestReadinessAssessment>()
            .HasOne<Plant>()
            .WithMany()
            .HasForeignKey(assessment => new { assessment.PlantId, assessment.FarmId })
            .HasPrincipalKey(plant => new { plant.Id, plant.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_harvest_readiness_plants_same_farm");

        modelBuilder.Entity<HarvestReadinessAssessment>()
            .HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(assessment => new { assessment.MissionId, assessment.FarmId })
            .HasPrincipalKey(mission => new { mission.Id, mission.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_harvest_readiness_missions_same_farm");

        modelBuilder.Entity<HarvestReadinessAssessment>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(assessment => assessment.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_harvest_readiness_users_reviewed_by");

        modelBuilder.Entity<FarmBaseMapVersion>()
            .HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(map => new { map.SourceSurveyOrderId, map.TenantId, map.FarmId })
            .HasPrincipalKey(order => new { order.Id, order.TenantId, order.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_base_map_versions_orders_same_tenant_farm");

        modelBuilder.Entity<FarmBaseMapVersion>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(map => map.PublishedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_farm_base_map_versions_users_published_by");

        modelBuilder.Entity<DroneMission>()
            .HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(mission => new { mission.SurveyOrderId, mission.TenantId, mission.FarmId })
            .HasPrincipalKey(order => new { order.Id, order.TenantId, order.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_drone_missions_orders_same_tenant_farm");

        modelBuilder.Entity<PreflightChecklistDefinition>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(definition => definition.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_preflight_definitions_users_created_by");

        modelBuilder.Entity<MissionPreflightChecklist>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(checklist => checklist.CompletedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_mission_preflight_checklists_users_completed_by");

        modelBuilder.Entity<MissionPlantObservation>()
            .HasOne<FarmBaseMapVersion>()
            .WithMany()
            .HasForeignKey(observation => new { observation.FarmBaseMapVersionId, observation.FarmId })
            .HasPrincipalKey(map => new { map.Id, map.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_observations_farm_base_map_same_farm");

        modelBuilder.Entity<PlantScan>()
            .HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(scan => new { scan.SurveyOrderId, scan.FarmId })
            .HasPrincipalKey(order => new { order.Id, order.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_scans_orders_same_farm");

        modelBuilder.Entity<PlantScan>()
            .HasOne<SurveyResult>()
            .WithMany()
            .HasForeignKey(scan => new { scan.SurveyResultId, scan.SurveyOrderId, scan.FarmId })
            .HasPrincipalKey(result => new { result.Id, result.SurveyOrderId, result.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plant_scans_results_same_order_farm");
    }
}

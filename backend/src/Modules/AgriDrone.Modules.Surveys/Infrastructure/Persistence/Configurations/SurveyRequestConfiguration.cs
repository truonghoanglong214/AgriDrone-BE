using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyRequestConfiguration : IEntityTypeConfiguration<SurveyRequest>
{
    public void Configure(EntityTypeBuilder<SurveyRequest> builder)
    {
        builder.ToTable(
            "survey_requests",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_requests_area_positive", "approximate_area_ha > 0");
                table.HasCheckConstraint("ck_survey_requests_pole_count", "estimated_pole_count IS NULL OR estimated_pole_count >= 0");
                table.HasCheckConstraint("ck_survey_requests_preferred_window", "preferred_end_at IS NULL OR (preferred_start_at IS NOT NULL AND preferred_end_at > preferred_start_at)");
                table.HasCheckConstraint(
                    "ck_survey_requests_kind_context",
                    "(kind = 'NEW_CUSTOMER'::system.survey_request_kind AND tenant_id IS NULL AND farm_id IS NULL AND requested_by_user_id IS NULL) OR " +
                    "(kind = 'EXISTING_TENANT_NEW_FARM'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NULL AND requested_by_user_id IS NOT NULL) OR " +
                    "(kind = 'EXISTING_FARM_SURVEY'::system.survey_request_kind AND tenant_id IS NOT NULL AND farm_id IS NOT NULL AND requested_by_user_id IS NOT NULL)");
            });
        builder.HasKey(request => request.Id).HasName("pk_survey_requests");
        builder.HasAlternateKey(request => new { request.Id, request.TenantId, request.FarmId }).HasName("uq_survey_requests_id_tenant_farm");
        builder.Property(request => request.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(request => request.RequestNumber).HasColumnName("request_number").HasColumnType("character varying(40)").HasMaxLength(40).IsRequired();
        builder.Property(request => request.Kind).HasColumnName("kind").HasColumnType("system.survey_request_kind").IsRequired();
        builder.Property(request => request.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(request => request.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(request => request.RequestedByUserId).HasColumnName("requested_by_user_id").HasColumnType("uuid");
        builder.Property(request => request.SurveyServiceId).HasColumnName("survey_service_id").HasColumnType("uuid");
        builder.Property(request => request.CallerScope).HasColumnName("caller_scope").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(request => request.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(request => request.ApplicantName).HasColumnName("applicant_name").HasColumnType("character varying(150)").HasMaxLength(150).IsRequired();
        builder.Property(request => request.ApplicantEmail).HasColumnName("applicant_email").HasColumnType("character varying(320)").HasMaxLength(320).IsRequired();
        builder.Property(request => request.ApplicantPhone).HasColumnName("applicant_phone").HasColumnType("character varying(30)").HasMaxLength(30).IsRequired();
        builder.Property(request => request.FarmName).HasColumnName("farm_name").HasColumnType("character varying(200)").HasMaxLength(200).IsRequired();
        builder.Property(request => request.FarmAddress).HasColumnName("farm_address").HasColumnType("text").IsRequired();
        builder.Property(request => request.ApproximateAreaHa).HasColumnName("approximate_area_ha").HasColumnType("numeric(12,4)").HasPrecision(12, 4);
        builder.Property(request => request.MapLocation).HasColumnName("map_location").HasColumnType("geometry(Point,4326)").IsRequired();
        builder.Property(request => request.EstimatedPoleCount).HasColumnName("estimated_pole_count").HasColumnType("integer");
        builder.Property(request => request.PreferredStartAt).HasColumnName("preferred_start_at").HasColumnType("timestamp with time zone");
        builder.Property(request => request.PreferredEndAt).HasColumnName("preferred_end_at").HasColumnType("timestamp with time zone");
        builder.Property(request => request.Notes).HasColumnName("notes").HasColumnType("text");
        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasColumnType("system.survey_request_status")
            .HasSentinel((SurveyRequestStatus)(-1))
            .HasDefaultValueSql("'SUBMITTED'::system.survey_request_status")
            .IsRequired();
        builder.Property(request => request.Version).IsRowVersion();
        builder.Property(request => request.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(request => request.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(request => request.RequestNumber).HasDatabaseName("uq_survey_requests_number").IsUnique();
        builder.HasIndex(request => new { request.CallerScope, request.IdempotencyKey }).HasDatabaseName("uq_survey_requests_caller_idempotency").IsUnique();
        builder.HasIndex(request => new { request.Status, request.CreatedAt }).HasDatabaseName("ix_survey_requests_inbox");
        builder.HasIndex(request => new { request.TenantId, request.CreatedAt }).HasDatabaseName("ix_survey_requests_tenant_history").IsDescending(false, true);
        builder.HasIndex(request => request.MapLocation).HasDatabaseName("ix_survey_requests_map_location_gist").HasMethod("gist");
        builder.HasOne(request => request.SurveyService).WithMany().HasForeignKey(request => request.SurveyServiceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_requests_services_service_id");
    }
}


using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class BoundaryCaptureSessionConfiguration
    : IEntityTypeConfiguration<BoundaryCaptureSession>
{
    public void Configure(EntityTypeBuilder<BoundaryCaptureSession> builder)
    {
        builder.ToTable("boundary_capture_sessions", "survey");
        builder.HasKey(item => item.Id).HasName("pk_boundary_capture_sessions");
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(item => item.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(item => item.StartOperationId).HasColumnName("start_operation_id").HasColumnType("uuid");
        builder.Property(item => item.FarmId).HasColumnName("farm_id").HasColumnType("uuid");
        builder.Property(item => item.AppointmentId).HasColumnName("appointment_id").HasColumnType("uuid");
        builder.Property(item => item.AppointmentVersion).HasColumnName("appointment_version");
        builder.Property(item => item.SystemManagerProfileId).HasColumnName("system_manager_profile_id").HasColumnType("uuid");
        builder.Property(item => item.DroneId).HasColumnName("drone_id").HasColumnType("uuid");
        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(32);
        builder.Property(item => item.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid");
        builder.Property(item => item.FinalizationEvidenceReference).HasColumnName("finalization_evidence_reference").HasMaxLength(1000);
        builder.Property(item => item.StartedBy).HasColumnName("started_by").HasColumnType("uuid");
        builder.Property(item => item.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.FinalizedBy).HasColumnName("finalized_by").HasColumnType("uuid");
        builder.Property(item => item.FinalizedAt).HasColumnName("finalized_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.AbortedBy).HasColumnName("aborted_by").HasColumnType("uuid");
        builder.Property(item => item.AbortedAt).HasColumnName("aborted_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.AbortReason).HasColumnName("abort_reason").HasMaxLength(2000);
        builder.Property(item => item.Version).IsRowVersion();
        builder.HasIndex(item => item.SurveyOrderId)
            .HasFilter("status = 'InProgress'")
            .HasDatabaseName("uq_boundary_capture_sessions_active_order")
            .IsUnique();
        builder.HasIndex(item => new { item.SurveyOrderId, item.StartOperationId })
            .HasDatabaseName("uq_boundary_capture_sessions_order_operation")
            .IsUnique();
        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(item => item.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_boundary_capture_sessions_orders_order_id");
        builder.HasOne<SurveyAppointment>()
            .WithMany()
            .HasForeignKey(item => item.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_boundary_capture_sessions_appointments_appointment_id");
        builder.HasMany(item => item.Samples)
            .WithOne(item => item.Session)
            .HasForeignKey(item => item.BoundaryCaptureSessionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_boundary_capture_samples_sessions_session_id");
        builder.Navigation(item => item.Samples)
            .HasField("_samples")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class BoundaryCaptureSampleConfiguration
    : IEntityTypeConfiguration<BoundaryCaptureSample>
{
    public void Configure(EntityTypeBuilder<BoundaryCaptureSample> builder)
    {
        builder.ToTable("boundary_capture_samples", "survey", table =>
        {
            table.HasCheckConstraint(
                "ck_boundary_capture_samples_coordinates",
                "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
            table.HasCheckConstraint(
                "ck_boundary_capture_samples_accuracy",
                "horizontal_accuracy_meters > 0");
        });
        builder.HasKey(item => item.Id).HasName("pk_boundary_capture_samples");
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(item => item.BoundaryCaptureSessionId).HasColumnName("boundary_capture_session_id").HasColumnType("uuid");
        builder.Property(item => item.ClientOperationId).HasColumnName("client_operation_id").HasColumnType("uuid");
        builder.Property(item => item.SequenceNumber).HasColumnName("sequence_number");
        builder.Property(item => item.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        builder.Property(item => item.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        builder.Property(item => item.HorizontalAccuracyMeters).HasColumnName("horizontal_accuracy_meters").HasPrecision(8, 2);
        builder.Property(item => item.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64);
        builder.Property(item => item.CapturedAt).HasColumnName("captured_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(item => new { item.BoundaryCaptureSessionId, item.SequenceNumber })
            .HasDatabaseName("uq_boundary_capture_samples_session_sequence")
            .IsUnique();
        builder.HasIndex(item => new { item.BoundaryCaptureSessionId, item.ClientOperationId })
            .HasDatabaseName("uq_boundary_capture_samples_session_operation")
            .IsUnique();
    }
}

public sealed class BaselineRoutePlanReferenceConfiguration
    : IEntityTypeConfiguration<BaselineRoutePlanReference>
{
    public void Configure(EntityTypeBuilder<BaselineRoutePlanReference> builder)
    {
        builder.ToTable("baseline_route_plan_references", "survey");
        builder.HasKey(item => item.Id).HasName("pk_baseline_route_plan_references");
        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(item => item.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(item => item.BoundaryCaptureSessionId).HasColumnName("boundary_capture_session_id").HasColumnType("uuid");
        builder.Property(item => item.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid");
        builder.Property(item => item.RoutePlanId).HasColumnName("route_plan_id").HasColumnType("uuid");
        builder.Property(item => item.RoutePlanVersion).HasColumnName("route_plan_version");
        builder.Property(item => item.SafetyInsetMeters).HasColumnName("safety_inset_meters").HasPrecision(8, 2);
        builder.Property(item => item.IsWithinSafetyInset).HasColumnName("is_within_safety_inset");
        builder.Property(item => item.Status).HasConversion<string>().HasColumnName("status").HasMaxLength(32);
        builder.Property(item => item.ApprovedBy).HasColumnName("approved_by").HasColumnType("uuid");
        builder.Property(item => item.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(item => item.RoutePlanId)
            .HasDatabaseName("uq_baseline_route_plan_references_route")
            .IsUnique();
        builder.HasIndex(item => new { item.SurveyOrderId, item.Status })
            .HasDatabaseName("ix_baseline_route_plan_references_order_status");
        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(item => item.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_baseline_route_plan_references_orders_order_id");
        builder.HasOne<BoundaryCaptureSession>()
            .WithMany()
            .HasForeignKey(item => item.BoundaryCaptureSessionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_baseline_route_plan_references_capture_session_id");
    }
}

using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyRequestTrackingConfiguration : IEntityTypeConfiguration<SurveyRequestTracking>
{
    public void Configure(EntityTypeBuilder<SurveyRequestTracking> builder)
    {
        builder.ToTable("survey_request_tracking", "survey");

        builder.HasKey(t => t.SurveyRequestId).HasName("pk_survey_request_tracking");
        builder.Property(t => t.SurveyRequestId).HasColumnName("survey_request_id").HasColumnType("uuid");
        builder.Property(t => t.TrackingTokenHash).HasColumnName("tracking_token_hash").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(t => t.EncryptedToken).HasColumnName("encrypted_token").HasColumnType("text").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(t => t.TrackingTokenHash)
            .HasDatabaseName("uq_survey_request_tracking_hash")
            .IsUnique();

        builder.HasOne<SurveyRequest>()
            .WithMany()
            .HasForeignKey(t => t.SurveyRequestId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_request_tracking_request");
    }
}

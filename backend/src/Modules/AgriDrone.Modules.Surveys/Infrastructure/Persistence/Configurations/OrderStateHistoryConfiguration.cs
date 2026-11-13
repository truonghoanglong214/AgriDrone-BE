using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class OrderStateHistoryConfiguration : IEntityTypeConfiguration<OrderStateHistory>
{
    public void Configure(EntityTypeBuilder<OrderStateHistory> builder)
    {
        builder.ToTable("order_state_history", "survey");

        builder.HasKey(h => h.Id).HasName("pk_order_state_history");
        builder.Property(h => h.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(h => h.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(h => h.OrderVersion).HasColumnName("order_version").HasColumnType("bigint").IsRequired();
        builder.Property(h => h.FromStatus).HasColumnName("from_status").HasColumnType("system.survey_order_status").IsRequired(false);
        builder.Property(h => h.ToStatus).HasColumnName("to_status").HasColumnType("system.survey_order_status").IsRequired();
        builder.Property(h => h.ActorId).HasColumnName("actor_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(h => h.Source).HasColumnName("source").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(h => h.Reason).HasColumnName("reason").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(h => h.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);
        builder.Property(h => h.CorrelationId).HasColumnName("correlation_id").HasColumnType("uuid").IsRequired();
        builder.Property(h => h.PredecessorHistoryId).HasColumnName("predecessor_history_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(h => new { h.SurveyOrderId, h.OrderVersion })
            .HasDatabaseName("ix_order_state_history_order_version");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(h => h.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_state_history_order");

        builder.HasOne<OrderStateHistory>()
            .WithMany()
            .HasForeignKey(h => h.PredecessorHistoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_state_history_predecessor");
    }
}

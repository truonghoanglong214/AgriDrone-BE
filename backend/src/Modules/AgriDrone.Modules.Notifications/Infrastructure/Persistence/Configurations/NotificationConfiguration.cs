using AgriDrone.Modules.Notifications.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable(
            "notifications",
            "notification",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "In-app notifications such as severe disease, task assignment, or processing completion.");
                tableBuilder.HasCheckConstraint(
                    "ck_notification_read_time",
                    "(is_read = FALSE AND read_at IS NULL) OR " +
                    "(is_read = TRUE AND read_at IS NOT NULL)");
            });

        builder.HasKey(notification => notification.Id).HasName("pk_notifications");

        builder.Property(notification => notification.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(notification => notification.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(notification => notification.NotificationType)
            .HasColumnName("notification_type")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasColumnName("title")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(notification => notification.Message)
            .HasColumnName("message")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(notification => notification.EntityType)
            .HasColumnName("entity_type")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50);

        builder.Property(notification => notification.EntityId)
            .HasColumnName("entity_id")
            .HasColumnType("uuid");

        builder.Property(notification => notification.IsRead)
            .HasColumnName("is_read")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(notification => notification.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(notification => notification.ReadAt)
            .HasColumnName("read_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt })
            .HasDatabaseName("ix_notifications_user_unread")
            .HasFilter("is_read = FALSE")
            .IsDescending(false, true);
    }
}

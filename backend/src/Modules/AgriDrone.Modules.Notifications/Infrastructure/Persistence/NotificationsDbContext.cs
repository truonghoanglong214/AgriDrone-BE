using AgriDrone.Modules.Notifications.Domain.Notifications;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Notifications.Infrastructure.Persistence;

internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notification");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }
}

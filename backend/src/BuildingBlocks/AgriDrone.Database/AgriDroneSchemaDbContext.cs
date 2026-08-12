using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.Harvests.Domain.Seasons;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Notifications.Domain.Notifications;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.SharedInfrastructure.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

public sealed class AgriDroneSchemaDbContext(DbContextOptions<AgriDroneSchemaDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("citext");

        PostgreSqlEnumMappings.ConfigureModel(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(User).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Farm).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DroneMission).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Plant).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Season).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FieldTask).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Notification).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditLog).Assembly);

        CrossModuleRelationshipConfiguration.Configure(modelBuilder);
    }
}

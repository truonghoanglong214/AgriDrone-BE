using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Notifications.Domain.Notifications;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Surveys.Domain;
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
        modelBuilder.HasPostgresExtension("btree_gist");
        PostgreSqlEnumMappings.ConfigureModel(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(User).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Farm).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DroneMission).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Plant).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Notification).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurveyService).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditLog).Assembly);

        CrossModuleRelationshipConfiguration.Configure(modelBuilder);
        Health.PlantHealthRelationships.Configure(modelBuilder);
        modelBuilder.Entity<Notification>().HasOne<AgriDrone.Modules.Surveys.Domain.PlantHealthPublication>().WithMany()
            .HasForeignKey(n => n.SourcePublicationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_health_publication");
        modelBuilder.Entity<DroneMission>().HasOne<AgriDrone.Modules.Surveys.Domain.PlantHealthPublication>().WithMany()
            .HasForeignKey(m => m.HealthPublicationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mission_health_publication");
    }
}

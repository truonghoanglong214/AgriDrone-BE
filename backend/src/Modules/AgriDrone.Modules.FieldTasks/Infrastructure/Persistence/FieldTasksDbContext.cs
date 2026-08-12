using AgriDrone.Modules.FieldTasks.Domain.Assignments;
using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.FieldTasks.Domain.Media;
using AgriDrone.Modules.FieldTasks.Domain.Updates;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.FieldTasks.Infrastructure.Persistence;

internal sealed class FieldTasksDbContext(DbContextOptions<FieldTasksDbContext> options)
    : DbContext(options)
{
    public DbSet<FieldTask> FieldTasks => Set<FieldTask>();

    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();

    public DbSet<TaskUpdate> TaskUpdates => Set<TaskUpdate>();

    public DbSet<TaskMedia> TaskMedia => Set<TaskMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("field_task");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FieldTasksDbContext).Assembly);
    }
}

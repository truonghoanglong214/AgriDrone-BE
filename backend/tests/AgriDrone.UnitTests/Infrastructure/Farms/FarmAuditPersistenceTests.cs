using AgriDrone.Modules.Farms;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Farms;

public sealed class FarmAuditPersistenceTests
{
    [Fact]
    public async Task FarmUnitOfWorkUsesFarmContextAsAuditSink()
    {
        var services = new ServiceCollection();
        services.AddFarmsModule(CreateConfiguration());

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<FarmsDbContext>();
        var unitOfWork =
            scope.ServiceProvider.GetRequiredService<IFarmUnitOfWork>();

        Assert.Same(context, unitOfWork);
        Assert.IsAssignableFrom<IAuditLogSink>(unitOfWork);
    }

    [Fact]
    public void FarmContextMapsAuditLogAndSameTenantFarmRelationship()
    {
        var services = new ServiceCollection();
        services.AddFarmsModule(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FarmsDbContext>();

        var auditEntity = context.Model.FindEntityType(typeof(AuditLog));

        Assert.NotNull(auditEntity);
        Assert.Equal("audit_logs", auditEntity.GetTableName());
        Assert.Equal("system", auditEntity.GetSchema());

        var farmForeignKey = Assert.Single(
            auditEntity.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Farm));

        Assert.Equal(DeleteBehavior.Restrict, farmForeignKey.DeleteBehavior);
        Assert.Equal(
            [nameof(AuditLog.FarmId), nameof(AuditLog.TenantId)],
            farmForeignKey.Properties.Select(property => property.Name));
        Assert.Equal(
            [nameof(Farm.Id), nameof(Farm.TenantId)],
            farmForeignKey.PrincipalKey.Properties.Select(property => property.Name));
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=localhost;Database=agridrone;Username=test;Password=test"
            })
            .Build();
}

using System.Data;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence;

public static class SurveyServiceV3Seeder
{
    private static readonly DateTimeOffset SeedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly (string Code, string Name, string Description)[] DefaultV3Services =
    [
        ("MAP", "Orthophoto Mapping", "High-resolution orthophoto and pole mapping for dragon fruit farms."),
        ("COMBO", "Comprehensive Health & Mapping", "Integrated orthophoto pole mapping with multi-spectral disease screening.")
    ];

    public static void Seed(DbContext dbContext)
    {
        if (!IsV3SchemaReady(dbContext))
        {
            return;
        }

        ReloadPostgreSqlTypes(dbContext);

        var services = dbContext.Set<SurveyService>();

        foreach (var def in DefaultV3Services)
        {
            var existing = services.SingleOrDefault(s => s.Code == def.Code);
            if (existing is null)
            {
                var service = SurveyService.CreateV3(def.Code, def.Name, def.Description, SeedTime);
                service.Activate(SeedTime, expectedVersion: 0);
                services.Add(service);
            }
        }

        dbContext.SaveChanges();
    }

    public static async Task SeedAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await IsV3SchemaReadyAsync(dbContext, cancellationToken))
        {
            return;
        }

        await ReloadPostgreSqlTypesAsync(dbContext, cancellationToken);

        var services = dbContext.Set<SurveyService>();

        foreach (var def in DefaultV3Services)
        {
            var existing = await services.SingleOrDefaultAsync(
                s => s.Code == def.Code,
                cancellationToken);

            if (existing is null)
            {
                var service = SurveyService.CreateV3(def.Code, def.Name, def.Description, SeedTime);
                service.Activate(SeedTime, expectedVersion: 0);
                services.Add(service);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsV3SchemaReady(DbContext dbContext)
    {
        if (!dbContext.Database.IsRelational())
        {
            return true;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = false;
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
            shouldClose = true;
        }

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'survey'
                  AND table_name = 'survey_services'
                  AND column_name = 'scope'
                LIMIT 1;
                """;
            var isReady = cmd.ExecuteScalar() is not null;
            if (isReady && connection is NpgsqlConnection npgsqlConnection)
            {
                npgsqlConnection.ReloadTypes();
            }

            return isReady;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static async Task<bool> IsV3SchemaReadyAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return true;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            shouldClose = true;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'survey'
                  AND table_name = 'survey_services'
                  AND column_name = 'scope'
                LIMIT 1;
                """;
            var isReady = await cmd.ExecuteScalarAsync(cancellationToken) is not null;
            if (isReady && connection is NpgsqlConnection npgsqlConnection)
            {
                await npgsqlConnection.ReloadTypesAsync(cancellationToken);
            }

            return isReady;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void ReloadPostgreSqlTypes(DbContext dbContext)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        if (dbContext.Database.GetDbConnection() is not NpgsqlConnection connection)
        {
            return;
        }

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            connection.ReloadTypes();
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static async Task ReloadPostgreSqlTypesAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        if (dbContext.Database.GetDbConnection() is not NpgsqlConnection connection)
        {
            return;
        }

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await connection.ReloadTypesAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}

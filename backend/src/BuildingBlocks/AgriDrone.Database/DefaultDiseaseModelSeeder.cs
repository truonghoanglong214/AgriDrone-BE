using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

internal static class DefaultDiseaseModelSeeder
{
    public static readonly Guid DefaultModelId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public const string DefaultModelName = "best_disease.pt (Disease-Detection)";
    public const string DefaultModelVersion = "1.0.0";

    private static readonly DateTimeOffset SeedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string SchemaProbe = """
        SELECT to_regclass('mission.ai_model_versions') IS NOT NULL AS "Value"
        """;

    public static void Seed(DbContext dbContext)
    {
        if (dbContext.Database.IsRelational() &&
            !dbContext.Database.SqlQueryRaw<bool>(SchemaProbe).Single())
        {
            return;
        }

        var models = dbContext.Set<AiModelVersion>();
        if (models.Any(m => m.ModelType == AiModelType.DiseaseDetection && m.IsActive) ||
            models.Any(m => m.ModelName == DefaultModelName && m.Version == DefaultModelVersion))
        {
            return;
        }

        models.Add(CreateDefaultModel());
        dbContext.SaveChanges();
    }

    public static async Task SeedAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational() &&
            !await dbContext.Database.SqlQueryRaw<bool>(SchemaProbe).SingleAsync(cancellationToken))
        {
            return;
        }

        var models = dbContext.Set<AiModelVersion>();
        if (await models.AnyAsync(m => m.ModelType == AiModelType.DiseaseDetection && m.IsActive, cancellationToken) ||
            await models.AnyAsync(m => m.ModelName == DefaultModelName && m.Version == DefaultModelVersion, cancellationToken))
        {
            return;
        }

        models.Add(CreateDefaultModel());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AiModelVersion CreateDefaultModel()
    {
        using var metrics = JsonSerializer.SerializeToDocument(new
        {
            datasetId = "dragonfruit_health_v1",
            datasetVersion = "1.0.0",
            annotationRuleVersion = "AGRIDRONE_ANNOTATION_RULES_V1",
            boundaryPolicyVersion = "STRICT_BOUNDARY_V1",
            teamLabelLimitation = "Team-labelled pilot observations; preliminary disease screening only.",
            comboInferencePolicy = new
            {
                version = "combo-v1",
                confidence = 0.25,
                clusterRadiusMeters = 15.0,
                bufferMeters = 3.0
            }
        });

        return AiModelVersion.CreateDiseaseDetectionModel(
            DefaultModelId,
            DefaultModelName,
            DefaultModelVersion,
            metrics,
            SeedTime);
    }
}

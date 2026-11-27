using System.Globalization;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgriDrone.Database;

internal static class DefaultSpatialPolicySeeder
{
    private const string ConfigurationPath = "DatabaseInitialization:DefaultSpatialPolicy";
    private const string SchemaProbe = """
        SELECT to_regclass('survey.spatial_policies') IS NOT NULL AS "Value"
        """;

    private static readonly string[] EffectiveFromFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFzzz"
    ];

    public static void Seed(DbContext dbContext, IConfiguration? configuration)
    {
        var policy = CreateConfiguredPolicy(configuration);
        if (policy is null || !dbContext.Database.SqlQueryRaw<bool>(SchemaProbe).Single())
        {
            return;
        }

        var policies = dbContext.Set<SpatialPolicy>();
        if (!policies.Any(existing => existing.StreamKey == SpatialPolicy.DefaultStreamKey))
        {
            policies.Add(policy);
            dbContext.SaveChanges();
        }
    }

    public static async Task SeedAsync(
        DbContext dbContext,
        IConfiguration? configuration,
        CancellationToken cancellationToken)
    {
        var policy = CreateConfiguredPolicy(configuration);
        if (policy is null || !await dbContext.Database.SqlQueryRaw<bool>(SchemaProbe).SingleAsync(cancellationToken))
        {
            return;
        }

        var policies = dbContext.Set<SpatialPolicy>();
        if (!await policies.AnyAsync(existing => existing.StreamKey == SpatialPolicy.DefaultStreamKey, cancellationToken))
        {
            policies.Add(policy);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static SpatialPolicy? CreateConfiguredPolicy(IConfiguration? configuration)
    {
        var section = configuration?.GetSection(ConfigurationPath);
        if (section is null || !section.Exists())
        {
            return null;
        }

        var effectiveFromText = section["EffectiveFrom"]?.Trim();
        if (effectiveFromText?.EndsWith('Z') == true)
        {
            effectiveFromText = effectiveFromText[..^1] + "+00:00";
        }

        if (!DateTimeOffset.TryParseExact(
                effectiveFromText, EffectiveFromFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var effectiveFrom)
            || effectiveFrom == default
            || effectiveFrom.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{ConfigurationPath}:EffectiveFrom must be an approved UTC timestamp with at most six fractional digits.");
        }

        if (!Guid.TryParse(section["CreatedBy"], out var createdBy) || createdBy == Guid.Empty)
        {
            throw new InvalidOperationException($"{ConfigurationPath}:CreatedBy must be a non-empty actor UUID.");
        }

        var evidenceReference = section["EvidenceReference"];
        if (string.IsNullOrWhiteSpace(evidenceReference))
        {
            throw new InvalidOperationException($"{ConfigurationPath}:EvidenceReference must identify the policy approval.");
        }

        return SpatialPolicy.CreateDefault(effectiveFrom, createdBy, evidenceReference);
    }
}

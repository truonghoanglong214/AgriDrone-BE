using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal sealed class SurveyCatalogueQueries(SurveysDbContext context)
    : ISurveyCatalogueQueries
{
    public async Task<IReadOnlyList<PublicSurveyServiceCatalogueItem>>
        GetPublicCatalogueAsync(
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
    {
        EnsureUtc(evaluatedAt, nameof(evaluatedAt));

        var services = await context.SurveyServices
            .AsNoTracking()
            .Where(service =>
                service.Status == SurveyServiceStatus.Active ||
                service.Status == SurveyServiceStatus.Experimental)
            .OrderBy(service => service.Code)
            .Select(service => new ServiceRow(
                service.Id,
                service.Code,
                service.Name,
                service.Description,
                service.ServiceType,
                service.Status,
                service.Version,
                service.CreatedAt,
                service.UpdatedAt))
            .ToListAsync(cancellationToken);

        if (services.Count == 0)
        {
            return [];
        }

        var serviceIds = services.Select(service => service.Id).ToArray();
        var effectivePrices = await context.SurveyServicePrices
            .AsNoTracking()
            .Where(price =>
                serviceIds.Contains(price.SurveyServiceId) &&
                price.PricePerPole != null &&
                price.Currency == "VND" &&
                price.EffectiveFrom <= evaluatedAt &&
                (!price.EffectiveTo.HasValue ||
                 evaluatedAt < price.EffectiveTo.Value))
            .OrderBy(price => price.SurveyServiceId)
            .ThenByDescending(price => price.EffectiveFrom)
            .ThenByDescending(price => price.Id)
            .Select(price => new PriceRow(
                price.Id,
                price.SurveyServiceId,
                price.PricePerPole,
                price.PricePerHa,
                price.Currency,
                price.EffectiveFrom,
                price.EffectiveTo,
                price.CreatedBy,
                price.CreatedAt))
            .ToListAsync(cancellationToken);

        EnsureAtMostOneEffectivePrice(effectivePrices, evaluatedAt);
        var pricesByService = effectivePrices.ToDictionary(
            price => price.SurveyServiceId);

        return services
            .Where(service => pricesByService.ContainsKey(service.Id))
            .Select(service =>
            {
                var price = pricesByService[service.Id];
                return new PublicSurveyServiceCatalogueItem(
                    service.Id,
                    service.Code,
                    service.Name,
                    service.Description,
                    service.ServiceType,
                    service.Status,
                    price.Id,
                    price.PricePerPole!.Value.Amount,
                    price.Currency,
                    price.EffectiveFrom,
                    price.EffectiveTo);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<SurveyServiceCatalogueItem>>
        GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        var services = await context.SurveyServices
            .AsNoTracking()
            .OrderBy(service => service.Code)
            .Select(service => new ServiceRow(
                service.Id,
                service.Code,
                service.Name,
                service.Description,
                service.ServiceType,
                service.Status,
                service.Version,
                service.CreatedAt,
                service.UpdatedAt))
            .ToListAsync(cancellationToken);

        var prices = await LoadPricesAsync(
            surveyServiceId: null,
            cancellationToken);
        var pricesByService = prices
            .GroupBy(price => price.SurveyServiceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SurveyServicePriceHistoryItem>)
                    group.Select(ToHistoryItem).ToArray());

        return services.Select(service => new SurveyServiceCatalogueItem(
                service.Id,
                service.Code,
                service.Name,
                service.Description,
                service.ServiceType,
                service.Status,
                service.Version,
                service.CreatedAt,
                service.UpdatedAt,
                pricesByService.GetValueOrDefault(service.Id) ?? []))
            .ToArray();
    }

    public async Task<IReadOnlyList<SurveyServicePriceHistoryItem>>
        GetPriceHistoryAsync(
            Guid surveyServiceId,
            CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            surveyServiceId,
            Guid.Empty);

        var prices = await LoadPricesAsync(
            surveyServiceId,
            cancellationToken);
        return prices.Select(ToHistoryItem).ToArray();
    }

    public async Task<EffectiveSurveyServicePrice?>
        GetEffectivePerPolePriceAsync(
            Guid surveyServiceId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            surveyServiceId,
            Guid.Empty);
        EnsureUtc(evaluatedAt, nameof(evaluatedAt));

        var prices = await context.SurveyServicePrices
            .AsNoTracking()
            .Where(price =>
                price.SurveyServiceId == surveyServiceId &&
                price.PricePerPole != null &&
                price.EffectiveFrom <= evaluatedAt &&
                (!price.EffectiveTo.HasValue ||
                 evaluatedAt < price.EffectiveTo.Value))
            .OrderByDescending(price => price.EffectiveFrom)
            .ThenByDescending(price => price.Id)
            .Take(2)
            .Select(price => new PriceRow(
                price.Id,
                price.SurveyServiceId,
                price.PricePerPole,
                price.PricePerHa,
                price.Currency,
                price.EffectiveFrom,
                price.EffectiveTo,
                price.CreatedBy,
                price.CreatedAt))
            .ToListAsync(cancellationToken);

        EnsureAtMostOneEffectivePrice(prices, evaluatedAt);
        if (prices.Count == 0)
        {
            return null;
        }

        var price = prices[0];
        return new EffectiveSurveyServicePrice(
            price.Id,
            price.SurveyServiceId,
            price.PricePerPole!.Value.Amount,
            price.Currency,
            price.EffectiveFrom,
            price.EffectiveTo);
    }

    public Task<bool> HasOverlappingPerPolePriceAsync(
        Guid surveyServiceId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? excludingPriceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            surveyServiceId,
            Guid.Empty);
        EnsureUtc(effectiveFrom, nameof(effectiveFrom));
        if (effectiveTo.HasValue)
        {
            EnsureUtc(effectiveTo.Value, nameof(effectiveTo));
        }

        return context.SurveyServicePrices
            .AsNoTracking()
            .AnyAsync(price =>
                    price.SurveyServiceId == surveyServiceId &&
                    price.PricePerPole != null &&
                    (!excludingPriceId.HasValue ||
                     price.Id != excludingPriceId.Value) &&
                    (!effectiveTo.HasValue ||
                     price.EffectiveFrom < effectiveTo.Value) &&
                    (!price.EffectiveTo.HasValue ||
                     effectiveFrom < price.EffectiveTo.Value),
                cancellationToken);
    }

    private async Task<IReadOnlyList<PriceRow>> LoadPricesAsync(
        Guid? surveyServiceId,
        CancellationToken cancellationToken)
    {
        return await context.SurveyServicePrices
            .AsNoTracking()
            .Where(price =>
                !surveyServiceId.HasValue ||
                price.SurveyServiceId == surveyServiceId.Value)
            .OrderByDescending(price => price.EffectiveFrom)
            .ThenByDescending(price => price.Id)
            .Select(price => new PriceRow(
                price.Id,
                price.SurveyServiceId,
                price.PricePerPole,
                price.PricePerHa,
                price.Currency,
                price.EffectiveFrom,
                price.EffectiveTo,
                price.CreatedBy,
                price.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static SurveyServicePriceHistoryItem ToHistoryItem(PriceRow price) =>
        new(
            price.Id,
            price.SurveyServiceId,
            price.PricePerPole?.Amount,
            price.PricePerHa,
            price.Currency,
            price.EffectiveFrom,
            price.EffectiveTo,
            price.CreatedBy,
            price.CreatedAt);

    private static void EnsureAtMostOneEffectivePrice(
        IReadOnlyCollection<PriceRow> prices,
        DateTimeOffset evaluatedAt)
    {
        var duplicateServiceId = prices
            .GroupBy(price => price.SurveyServiceId)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateServiceId.HasValue)
        {
            throw new InvalidOperationException(
                $"Survey service '{duplicateServiceId}' has overlapping per-pole prices at '{evaluatedAt:O}'.");
        }
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be a non-default UTC value.",
                parameterName);
        }
    }

    private sealed record ServiceRow(
        Guid Id,
        string Code,
        string Name,
        string Description,
        SurveyServiceType ServiceType,
        SurveyServiceStatus Status,
        uint Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record PriceRow(
        Guid Id,
        Guid SurveyServiceId,
        PricePerPole? PricePerPole,
        decimal? PricePerHa,
        string Currency,
        DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo,
        Guid CreatedBy,
        DateTimeOffset CreatedAt);
}

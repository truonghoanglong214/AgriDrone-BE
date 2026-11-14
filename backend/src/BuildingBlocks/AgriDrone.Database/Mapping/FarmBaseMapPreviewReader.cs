using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDetail;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Database.Mapping;

internal sealed class FarmBaseMapPreviewReader(
    AgriDroneSchemaDbContext context,
    IObjectStorage storage,
    ILogger<FarmBaseMapPreviewReader> logger) : IFarmBaseMapPreviewReader
{
    public Task<bool> CanOwnerReadAsync(Guid tenantId, Guid farmId, Guid sourceOrderId,
        CancellationToken cancellationToken = default) =>
        context.Set<SurveyOrder>().AsNoTracking().AnyAsync(order =>
            order.Id == sourceOrderId && order.TenantId == tenantId && order.FarmId == farmId &&
            order.Scope == ContractScope.Legacy && order.Status != SurveyOrderStatus.Cancelled &&
            (!order.PostFlightSettlement ||
                (order.Status == SurveyOrderStatus.Completed && order.ServiceReviewedAt != null &&
                 order.FinalPrice > 0m && order.DepositPaymentStatus == SurveyPaymentStatus.Paid &&
                 (order.RemainingAmount == 0m || order.Payments.Any(payment =>
                     payment.Purpose == PaymentPurpose.FinalBalance && payment.Status == SurveyPaymentStatus.Paid &&
                     payment.Amount == order.RemainingAmount && payment.Currency == order.Currency)))), cancellationToken);

    public async Task<FarmBaseMapPreview?> GetAsync(
        Guid tenantId, Guid farmId, Guid missionId,
        CancellationToken cancellationToken = default)
    {
        // Only read existing output from the map's own source mission; never start processing.
        var preview = await context.Set<MissionMedia>().AsNoTracking()
            .Where(item => item.MissionId == missionId
                && item.Mission.TenantId == tenantId && item.Mission.FarmId == farmId
                && item.Media.TenantId == tenantId && item.Media.FarmId == farmId
                && item.MediaRole == MissionMediaRole.ProcessedImage
                && item.Media.MimeType == "image/jpeg"
                && item.Media.DeletedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Media.Url, item.Media.Metadata })
            .FirstOrDefaultAsync(cancellationToken);

        if (preview is null)
        {
            return null;
        }

        try
        {
            var url = await storage.CreateDownloadUriAsync(
                preview.Url, TimeSpan.FromMinutes(15), cancellationToken);
            return new FarmBaseMapPreview(url.ToString(), ReadBounds(preview.Metadata.RootElement));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Keep the boundary/poles readable when storage is unavailable. Do not log signed URLs.
            logger.LogWarning("Cannot sign base-map preview for mission {MissionId} ({FailureType})",
                missionId, exception.GetType().Name);
            return null;
        }
    }

    private static MapBoundsDto? ReadBounds(JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty("bounds", out var bounds)
            || bounds.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var minLat = ReadNumber(bounds, "min_lat", "MinLat");
        var maxLat = ReadNumber(bounds, "max_lat", "MaxLat");
        var minLon = ReadNumber(bounds, "min_lon", "MinLon");
        var maxLon = ReadNumber(bounds, "max_lon", "MaxLon");
        return minLat.HasValue && maxLat.HasValue && minLon.HasValue && maxLon.HasValue
            && minLat >= -90 && maxLat <= 90 && minLon >= -180 && maxLon <= 180
            && minLat < maxLat && minLon < maxLon
            ? new MapBoundsDto(minLat.Value, maxLat.Value, minLon.Value, maxLon.Value)
            : null;
    }

    private static double? ReadNumber(JsonElement element, string snakeCase, string pascalCase) =>
        (element.TryGetProperty(snakeCase, out var value) || element.TryGetProperty(pascalCase, out value))
        && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? number : null;
}

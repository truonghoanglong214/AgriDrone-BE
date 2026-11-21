using System.Text.Json;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal sealed record FinancialHistoryRow(long Sequence, string Source, Guid SourceId, Guid OrderId,
    DateTimeOffset RecordedAt, JsonElement Data, bool Deleted)
{
    public string? Text(string key) => Data.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null
        ? value.ToString() : null;
    public decimal Money(string key) => Data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
        ? value.GetDecimal() : 0m;
    public DateTimeOffset? Time(string key) => DateTimeOffset.TryParse(Text(key), System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out var value) ? value : null;
}

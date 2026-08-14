using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Observations;

public sealed class ObservationMatchCandidate : Entity
{
    private ObservationMatchCandidate()
    {
    }

    public Guid ObservationId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid PlantId { get; private set; }

    public MatchStrategy Strategy { get; private set; }

    public int CandidateRank { get; private set; }

    public decimal? GpsDistanceM { get; private set; }

    public int? RowDelta { get; private set; }

    public int? ColumnDelta { get; private set; }

    public decimal? GridScore { get; private set; }

    public decimal FinalScore { get; private set; }

    public string AlgorithmVersion { get; private set; } = null!;

    public JsonDocument Parameters { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
}

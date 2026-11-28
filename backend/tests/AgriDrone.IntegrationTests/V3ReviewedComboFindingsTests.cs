using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V3ReviewedComboFindingsTests
{
    [Fact]
    public void ReturnsOnlyReleasedImagesFromTheAcceptedFindingOnEachReviewedPole()
    {
        var firstPole = Guid.NewGuid();
        var secondPole = Guid.NewGuid();
        var firstRaw = Guid.NewGuid();
        var firstMarked = Guid.NewGuid();
        var firstCrop = Guid.NewGuid();
        var secondRaw = Guid.NewGuid();
        var secondUnreleasedCrop = Guid.NewGuid();
        var first = Finding(firstPole,
            new(firstRaw, "source_image"), new(firstMarked, "annotated_image"), new(firstCrop, "crop_image"));
        var second = Finding(null,
            new(secondRaw, "source_image"), new(secondUnreleasedCrop, "crop_image"));
        var excluded = Finding(firstPole, new EvidenceMediaReferenceV4(Guid.NewGuid(), "crop_image"));
        var unlinked = Finding(null, new EvidenceMediaReferenceV4(Guid.NewGuid(), "crop_image"));
        var reviewed = new V3ReviewedComboFindings(
            new ComboAnalysisReadyV1(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow, null, Guid.NewGuid(),
                null, null, null, default, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), "Preliminary", "Test fixture",
                new("dataset", "1", "1", Guid.NewGuid(), "1", null, "1"),
                [first, second, excluded, unlinked], [], []),
            new ComboReviewDecisionsSnapshotRecord(
                [
                    new(first.FindingId, SurveyComboReviewRevision.FindingDecisionAccept, null, []),
                    new(second.FindingId, SurveyComboReviewRevision.FindingDecisionAccept, null, [], secondPole),
                    new(excluded.FindingId, SurveyComboReviewRevision.FindingDecisionExclude, null, []),
                    new(unlinked.FindingId, SurveyComboReviewRevision.FindingDecisionAccept, null, [])
                ],
                [], []));

        var findingsByPole = reviewed.AcceptedFindings(new HashSet<Guid> { firstPole, secondPole })
            .ToDictionary(item => item.PlantProfileId, item => item.Finding);
        var released = new HashSet<Guid> { firstRaw, firstMarked, firstCrop, secondRaw, Guid.NewGuid() };

        Assert.Equal(2, findingsByPole.Count);
        Assert.Equal(new[] { firstCrop, firstMarked, firstRaw },
            V3ReviewedComboFindings.ReadEvidenceMediaIds(findingsByPole[firstPole], released));
        Assert.Equal(new[] { secondRaw },
            V3ReviewedComboFindings.ReadEvidenceMediaIds(findingsByPole[secondPole], released));
        Assert.Empty(V3ReviewedComboFindings.ReadEvidenceMediaIds(second, new HashSet<Guid> { firstRaw }));
    }

    private static ComboFindingV1 Finding(Guid? plantId, params EvidenceMediaReferenceV4[] images) =>
        new(Guid.NewGuid(), 1, plantId, null, new GeoJsonPointV3("Point", [106, 10]),
            DateTimeOffset.UtcNow, "SUNBURN", "Suspected", 0.9m, "Test fixture", [], images);
}

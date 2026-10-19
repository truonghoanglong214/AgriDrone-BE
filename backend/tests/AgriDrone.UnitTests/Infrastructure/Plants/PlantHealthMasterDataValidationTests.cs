using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Infrastructure.Initialization;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Plants;

public sealed class PlantHealthMasterDataValidationTests
{
    [Fact]
    public void ExactPilotLabelSetWithProvenanceIsValid()
    {
        var result = PlantHealthMasterDataRules.Validate(CreateValidLabels());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MissingRequiredLabelIsRejected()
    {
        var labels = CreateValidLabels()
            .Where(label => label.Code != "ANTHRACNOSE")
            .ToArray();

        var result = PlantHealthMasterDataRules.Validate(labels);

        Assert.Contains(
            "Required plant-health label 'ANTHRACNOSE' is missing.",
            result.Errors);
    }

    [Fact]
    public void InactiveRequiredLabelIsRejected()
    {
        var labels = CreateValidLabels()
            .Select(label => label.Code == "SUNBURN"
                ? label with { IsActive = false }
                : label)
            .ToArray();

        var result = PlantHealthMasterDataRules.Validate(labels);

        Assert.Contains(
            "Required plant-health label 'SUNBURN' must be active.",
            result.Errors);
    }

    [Fact]
    public void WrongKindOrProvenanceIsRejected()
    {
        var labels = CreateValidLabels()
            .Select(label => label.Code == "BROWN_SPOT"
                ? label with
                {
                    LabelKind = PlantHealthLabelKind.ConfusingNonDisease,
                    DatasetVersion = "OUTDATED"
                }
                : label)
            .ToArray();

        var result = PlantHealthMasterDataRules.Validate(labels);

        Assert.Contains(
            "Plant-health label 'BROWN_SPOT' has an invalid kind.",
            result.Errors);
        Assert.Contains(
            "Plant-health label 'BROWN_SPOT' has invalid provenance metadata.",
            result.Errors);
    }

    [Fact]
    public void UnexpectedPilotLabelIsRejected()
    {
        var labels = CreateValidLabels()
            .Append(CreateLabel(
                "UNAPPROVED",
                PlantHealthLabelKind.SuspectedDisease))
            .ToArray();

        var result = PlantHealthMasterDataRules.Validate(labels);

        Assert.Contains(
            "Unexpected pilot plant-health labels: UNAPPROVED.",
            result.Errors);
    }

    private static PlantHealthLabelSeedSnapshot[] CreateValidLabels() =>
    [
        CreateLabel("BROWN_SPOT", PlantHealthLabelKind.SuspectedDisease),
        CreateLabel("ANTHRACNOSE", PlantHealthLabelKind.SuspectedDisease),
        CreateLabel("SUNBURN", PlantHealthLabelKind.ConfusingNonDisease),
        CreateLabel("MECHANICAL_SCAR", PlantHealthLabelKind.ConfusingNonDisease)
    ];

    private static PlantHealthLabelSeedSnapshot CreateLabel(
        string code,
        PlantHealthLabelKind kind) => new(
        code,
        kind,
        PlantHealthLabelPolicy.DatasetVersion,
        PlantHealthLabelPolicy.AnnotationRuleVersion,
        PlantHealthLabelPolicy.TeamLabelLimitation,
        IsActive: true);
}

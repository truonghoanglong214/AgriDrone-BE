using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Guidance;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

public sealed class PlantHealthMasterDataTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("BROWN_SPOT")]
    [InlineData("ANTHRACNOSE")]
    public void LockedSuspectedDiseaseClassesCarryTeamLabelProvenance(string code)
    {
        var condition = PlantCondition.Create(
            code,
            code,
            null,
            ConditionType.Disease,
            null,
            Now,
            PlantHealthLabelKind.SuspectedDisease,
            PlantHealthLabelPolicy.DatasetVersion,
            PlantHealthLabelPolicy.AnnotationRuleVersion,
            PlantHealthLabelPolicy.TeamLabelLimitation);

        Assert.Equal(PlantHealthLabelKind.SuspectedDisease,
            condition.PlantHealthLabelKind);
        Assert.Equal(PlantHealthLabelPolicy.DatasetVersion,
            condition.DatasetVersion);
    }

    [Theory]
    [InlineData("SUNBURN", ConditionType.AbioticDamage)]
    [InlineData("MECHANICAL_SCAR", ConditionType.MechanicalDamage)]
    public void ConfusingNonDiseaseClassesCannotBecomeDiagnosisLabels(
        string code,
        ConditionType conditionType)
    {
        var condition = PlantCondition.Create(
            code,
            code,
            null,
            conditionType,
            null,
            Now,
            PlantHealthLabelKind.ConfusingNonDisease,
            PlantHealthLabelPolicy.DatasetVersion,
            PlantHealthLabelPolicy.AnnotationRuleVersion,
            PlantHealthLabelPolicy.TeamLabelLimitation);

        Assert.Equal(PlantHealthLabelKind.ConfusingNonDisease,
            condition.PlantHealthLabelKind);
    }

    [Fact]
    public void CustomDiseaseClassCanEnterTargetSet()
    {
        var condition = PlantCondition.Create(
            "STEM_CANKER",
            "Stem Canker",
            "Neoscytalidium dimidiatum",
            ConditionType.Disease,
            "Canker on stem",
            Now,
            PlantHealthLabelKind.SuspectedDisease);

        Assert.Equal("STEM_CANKER", condition.Code);
        Assert.Equal(PlantHealthLabelKind.SuspectedDisease, condition.PlantHealthLabelKind);
        Assert.Equal(PlantHealthLabelPolicy.DefaultDatasetVersion, condition.DatasetVersion);
    }

    [Fact]
    public void TargetClassCanUseCustomProvenance()
    {
        var condition = PlantCondition.Create(
            "MOSAIC_VIRUS",
            "Mosaic Virus",
            null,
            ConditionType.Disease,
            null,
            Now,
            PlantHealthLabelKind.SuspectedDisease,
            "CUSTOM_DATASET_V2",
            "CUSTOM_RULES_V2",
            "Custom limitation note");

        Assert.Equal("MOSAIC_VIRUS", condition.Code);
        Assert.Equal("CUSTOM_DATASET_V2", condition.DatasetVersion);
        Assert.Equal("CUSTOM_RULES_V2", condition.AnnotationRuleVersion);
        Assert.Equal("Custom limitation note", condition.TeamLabelLimitation);
    }

    [Fact]
    public void SourcedGuidanceIsVersionedCitedAndIndependentOfSeverity()
    {
        var condition = PlantCondition.Create(
            "BROWN_SPOT",
            "Brown spot",
            null,
            ConditionType.Disease,
            null,
            Now,
            PlantHealthLabelKind.SuspectedDisease,
            PlantHealthLabelPolicy.DatasetVersion,
            PlantHealthLabelPolicy.AnnotationRuleVersion,
            PlantHealthLabelPolicy.TeamLabelLimitation);
        var guidance = SourcedGuidance.CreateDraft(
            "BROWN_SPOT_FIELD_GUIDANCE",
            1,
            condition.Id,
            condition.RevisionNumber,
            condition.Code,
            "Published source",
            "https://example.invalid/source",
            "Inspect and document affected plants before taking action.",
            Now,
            null,
            Guid.NewGuid(),
            Now);

        guidance.Activate(Now.AddMinutes(1), 0);

        Assert.True(guidance.IsEffectiveAt(Now.AddMinutes(2)));
        Assert.DoesNotContain(
            typeof(SourcedGuidanceResponse).GetProperties(),
            property => property.Name.Contains("Severity", StringComparison.OrdinalIgnoreCase));
    }
}

using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

/// <summary>
/// Unit tests covering:
///   TC-F07-U01  – Invoice arithmetic correctness
///   TC-ST-NEG-07 – Stale-revision 409 conflict on duplicate Supersede
/// </summary>
public sealed class InvoiceRevisionStaleTokenTests
{
    // ------------------------------------------------------------------ //
    // Shared test-data helpers
    // ------------------------------------------------------------------ //

    private static readonly DateTimeOffset BaseAt =
        new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Builds a minimal valid CURRENT SurveyInvoiceRevision for the given
    /// pole count / price / deposit combination.
    /// </summary>
    private static SurveyInvoiceRevision BuildRevision(
        int poleCount,
        decimal pricePerPole,
        decimal deposit,
        int revisionNumber = 1,
        Guid? predecessorRevisionId = null,
        Guid? zeroPoleApprovedBy = null,
        string? zeroPoleReasonCode = null,
        string? zeroPoleEvidenceReference = null)
    {
        var id                        = Guid.NewGuid();
        var tenantId                  = Guid.NewGuid();
        var farmId                    = Guid.NewGuid();
        var surveyOrderId             = Guid.NewGuid();
        var approvedResultRevisionId  = Guid.NewGuid();
        var poleCountSnapshotId       = Guid.NewGuid();
        var surveyServicePriceId      = Guid.NewGuid();
        var commercialPolicyVersionId = Guid.NewGuid();
        var finalizedBy               = Guid.NewGuid();

        var totalAmount       = poleCount * pricePerPole;
        var dueAmount         = Math.Max(0m, totalAmount - deposit);
        var overpaymentAmount = Math.Max(0m, deposit - totalAmount);

        var fingerprint = SurveyInvoiceRevision.ComputeEvidenceFingerprint(
            tenantId:                   tenantId,
            surveyOrderId:              surveyOrderId,
            farmId:                     farmId,
            revisionNumber:             revisionNumber,
            approvedResultRevisionId:   approvedResultRevisionId,
            poleCountSnapshotId:        poleCountSnapshotId,
            verifiedBillablePoleCount:  poleCount,
            surveyServicePriceId:       surveyServicePriceId,
            pricePerPole:               pricePerPole,
            currency:                   "VND",
            commercialPolicyVersionId:  commercialPolicyVersionId,
            totalAmount:                totalAmount,
            validAllocatedDepositNet:   deposit,
            dueAmount:                  dueAmount,
            overpaymentAmount:          overpaymentAmount,
            finalizedBy:                finalizedBy,
            finalizedAt:                BaseAt,
            finalizationReason:         "unit-test finalization",
            predecessorRevisionId:      predecessorRevisionId,
            zeroPoleApprovedBy:         zeroPoleApprovedBy,
            zeroPoleReasonCode:         zeroPoleReasonCode,
            zeroPoleEvidenceReference:  zeroPoleEvidenceReference);

        var result = SurveyInvoiceRevision.Create(
            id:                         id,
            tenantId:                   tenantId,
            farmId:                     farmId,
            surveyOrderId:              surveyOrderId,
            revisionNumber:             revisionNumber,
            approvedResultRevisionId:   approvedResultRevisionId,
            poleCountSnapshotId:        poleCountSnapshotId,
            verifiedBillablePoleCount:  poleCount,
            surveyServicePriceId:       surveyServicePriceId,
            pricePerPole:               pricePerPole,
            currency:                   "VND",
            commercialPolicyVersionId:  commercialPolicyVersionId,
            validAllocatedDepositNet:   deposit,
            finalizedBy:                finalizedBy,
            finalizedAt:                BaseAt,
            finalizationReason:         "unit-test finalization",
            evidenceFingerprint:        fingerprint,
            predecessorRevisionId:      predecessorRevisionId,
            zeroPoleApprovedBy:         zeroPoleApprovedBy,
            zeroPoleReasonCode:         zeroPoleReasonCode,
            zeroPoleEvidenceReference:  zeroPoleEvidenceReference);

        Assert.True(result.IsSuccess, $"BuildRevision failed: {result.Error?.Code}");
        return result.Value;
    }

    // ------------------------------------------------------------------ //
    // TC-F07-U01-A  Invoice arithmetic – normal 150-pole case
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-F07-U01-A")]
    public void InvoiceWith150PolesHasCorrectArithmetic()
    {
        // Arrange / Act
        var revision = BuildRevision(
            poleCount:    150,
            pricePerPole: 1_000m,
            deposit:      100_000m);

        // Assert
        Assert.Equal(150_000m, revision.TotalAmount);
        Assert.Equal(50_000m,  revision.DueAmount);
        Assert.Equal(0m,       revision.OverpaymentAmount);
        Assert.Equal(SurveyInvoiceRevision.StatusCurrent, revision.Status);
    }

    // ------------------------------------------------------------------ //
    // TC-F07-U01-B  Invoice arithmetic – zero-pole exception
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-F07-U01-B")]
    public void ZeroPoleInvoiceHasZeroAmounts()
    {
        // Arrange / Act
        var revision = BuildRevision(
            poleCount:                0,
            pricePerPole:             1_000m,
            deposit:                  0m,
            zeroPoleApprovedBy:       Guid.NewGuid(),
            zeroPoleReasonCode:       "NO_CROP",
            zeroPoleEvidenceReference: "ref-evidence-001");

        // Assert
        Assert.Equal(0m, revision.TotalAmount);
        Assert.Equal(0m, revision.DueAmount);
    }

    // ------------------------------------------------------------------ //
    // TC-ST-NEG-07-A  Supersede a CURRENT revision → SUPERSEDED
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-NEG-07-A")]
    public void SupersedeCurrentRevisionSucceeds()
    {
        // Arrange
        var revision     = BuildRevision(50, 1_000m, 0m);
        var actorId      = Guid.NewGuid();
        var successorId  = Guid.NewGuid();
        var supersededAt = BaseAt.AddMinutes(5);

        // Act
        var result = revision.Supersede(actorId, supersededAt, "first supersede", successorId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SurveyInvoiceRevision.StatusSuperseded, revision.Status);
    }

    // ------------------------------------------------------------------ //
    // TC-ST-NEG-07-B  Supersede an already-SUPERSEDED revision with
    //                 different params → conflict (409)
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-NEG-07-B")]
    public void SupersedeAlreadySupersededWithDifferentParamsIsConflict()
    {
        // Arrange – bring revision to SUPERSEDED
        var revision     = BuildRevision(50, 1_000m, 0m);
        var actorId      = Guid.NewGuid();
        var successorId  = Guid.NewGuid();
        var supersededAt = BaseAt.AddMinutes(5);

        revision.Supersede(actorId, supersededAt, "original reason", successorId);

        // Act – attempt again with a *different* reason
        var result = revision.Supersede(
            actorId,
            supersededAt,
            reason:              "completely different reason",
            successorRevisionId: successorId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("AlreadySuperseded", result.Error!.Code,
            StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ //
    // TC-ST-NEG-07-C  Supersede with identical params → idempotent success
    // ------------------------------------------------------------------ //

    [Fact, Trait("TC", "TC-ST-NEG-07-C")]
    public void SupersedeIdempotentWithSameParamsIsSuccess()
    {
        // Arrange – bring revision to SUPERSEDED
        var revision     = BuildRevision(50, 1_000m, 0m);
        var actorId      = Guid.NewGuid();
        var successorId  = Guid.NewGuid();
        var supersededAt = BaseAt.AddMinutes(5);
        const string reason = "idempotent reason";

        revision.Supersede(actorId, supersededAt, reason, successorId);

        // Act – repeat with identical params
        var result = revision.Supersede(actorId, supersededAt, reason, successorId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SurveyInvoiceRevision.StatusSuperseded, revision.Status);
    }
}

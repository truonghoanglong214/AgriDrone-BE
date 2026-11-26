using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface IBalanceCheckoutTokenService
{
    string Protect(BalanceCheckoutTokenPayload payload);

    Result<BalanceCheckoutTokenPayload> UnprotectAndValidate(
        string checkoutToken,
        Guid expectedActorId,
        Guid expectedTenantId,
        Guid expectedOrderId);
}

public sealed record BalanceCheckoutTokenPayload(
    Guid ActorId,
    Guid TenantId,
    Guid FarmId,
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ExpectedInvoiceRevisionId,
    string ExpectedInvoiceFingerprint,
    Guid ExpectedApprovedResultRevisionId,
    string ExpectedApprovedResultFingerprint,
    Guid ExpectedCountSnapshotId,
    string ExpectedCountSnapshotFingerprint,
    DateTimeOffset IssuedAt);

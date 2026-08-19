namespace AgriDrone.SharedKernel.Application.Abstractions.Authorization;

public sealed record AccessDecision
{
    private AccessDecision(bool isAllowed, AccessDenialReason reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public bool IsAllowed { get; }

    public AccessDenialReason Reason { get; }

    public static AccessDecision Allow() =>
        new(true, AccessDenialReason.None);

    public static AccessDecision Deny(AccessDenialReason reason)
    {
        if (reason == AccessDenialReason.None)
        {
            throw new ArgumentException(
                "A denied access decision must include a denial reason.",
                nameof(reason));
        }

        return new AccessDecision(false, reason);
    }
}

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public sealed record QualityRefundClaimWindowView(
    DateTimeOffset ServerTime,
    DateTimeOffset? NoticeCreatedAt,
    DateTimeOffset? DeadlineExclusive,
    bool CanReport,
    string? Reason);

using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

/// <summary>SePay incoming bank-transfer webhook payload.</summary>
public sealed record SurveyPaymentCallbackRequest(
    [param: Required] long Id,
    string? Gateway,
    string? TransactionDate,
    string? AccountNumber,
    string? SubAccount,
    string? Code,
    string? Content,
    [param: Required] string TransferType,
    string? Description,
    [param: Required] decimal TransferAmount,
    decimal? Accumulated,
    string? ReferenceCode);

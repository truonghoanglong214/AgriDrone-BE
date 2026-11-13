using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;

internal sealed class CreateSurveyDepositPolicyCommandHandler(
    ISurveyDepositPolicyRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSurveyDepositPolicyCommand,
        Result<SurveyDepositPolicyResponse>>
{
    public async Task<Result<SurveyDepositPolicyResponse>> Handle(
        CreateSurveyDepositPolicyCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SurveyDepositPolicyResponse>(
                AppError.Forbidden("SurveyDeposit.Forbidden", "SystemAdmin is required."));
        }

        if (command.AmountVnd < 0m ||
            command.AmountVnd != decimal.Truncate(command.AmountVnd) ||
            command.ExpectedVersion < 0)
        {
            return Result.Failure<SurveyDepositPolicyResponse>(
                AppError.Validation("SurveyDeposit.InvalidAmount",
                    "Deposit must be a non-negative whole VND amount."));
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var current = await repository.GetCurrentForUpdateAsync(token);
                if ((current?.VersionNumber ?? 0) != command.ExpectedVersion)
                {
                    return Result.Failure<SurveyDepositPolicyResponse>(
                        AppError.Conflict("SurveyDeposit.VersionConflict",
                            "The deposit amount changed. Reload before saving."));
                }

                var effectiveAt = timeProvider.GetUtcNow();
                if (current is not null && effectiveAt <= current.EffectiveFrom)
                {
                    effectiveAt = current.EffectiveFrom.AddMilliseconds(1);
                }

                if (current is not null)
                {
                    current.CloseAt(effectiveAt);
                    // Release the unique current-policy slot inside this transaction.
                    await unitOfWork.SaveChangesAsync(token);
                }
                var policy = SurveyDepositPolicy.Create(
                    command.ExpectedVersion + 1,
                    command.AmountVnd,
                    actorId,
                    effectiveAt);
                repository.Add(policy);

                using var oldData = JsonSerializer.SerializeToDocument(new
                {
                    CurrentPolicyId = current?.Id,
                    CurrentAmountVnd = current?.AmountVnd
                });
                using var newData = JsonSerializer.SerializeToDocument(new
                {
                    policy.Id,
                    policy.VersionNumber,
                    policy.AmountVnd,
                    policy.EffectiveFrom
                });
                auditWriter.AddSystemAdminAction(
                    unitOfWork,
                    actorId,
                    executionContext.CorrelationId,
                    nameof(SurveyDepositPolicy),
                    policy.Id,
                    "CREATE_VERSION",
                    oldData,
                    newData,
                    effectiveAt);
                await unitOfWork.SaveChangesAsync(token);

                return Result.Success(new SurveyDepositPolicyResponse(
                    policy.Id,
                    policy.VersionNumber,
                    policy.AmountVnd,
                    policy.EffectiveFrom,
                    policy.EffectiveTo,
                    policy.CreatedBy));
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (DbUpdateException)
        {
            return Conflict();
        }
    }

    private static Result<SurveyDepositPolicyResponse> Conflict() =>
        Result.Failure<SurveyDepositPolicyResponse>(
            AppError.Conflict("SurveyDeposit.PersistenceConflict",
                "The deposit policy changed concurrently."));
}

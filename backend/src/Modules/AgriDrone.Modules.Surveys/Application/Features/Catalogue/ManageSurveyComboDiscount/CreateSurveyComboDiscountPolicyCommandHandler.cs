using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

internal sealed class CreateSurveyComboDiscountPolicyCommandHandler(
    ISurveyComboDiscountPolicyRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSurveyComboDiscountPolicyCommand,
        Result<SurveyComboDiscountPolicyResponse>>
{
    public async Task<Result<SurveyComboDiscountPolicyResponse>> Handle(
        CreateSurveyComboDiscountPolicyCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SurveyComboDiscountPolicyResponse>(
                AppError.Forbidden("SurveyComboDiscount.Forbidden", "SystemAdmin is required."));
        }

        if (command.DiscountPercentage < 0m ||
            command.DiscountPercentage > 100m ||
            command.ExpectedVersion < 0)
        {
            return Result.Failure<SurveyComboDiscountPolicyResponse>(
                AppError.Validation("SurveyComboDiscount.InvalidPercentage",
                    "Discount percentage must be between 0 and 100."));
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var current = await repository.GetCurrentForUpdateAsync(token);
                if ((current?.VersionNumber ?? 0) != command.ExpectedVersion)
                {
                    return Result.Failure<SurveyComboDiscountPolicyResponse>(
                        AppError.Conflict("SurveyComboDiscount.VersionConflict",
                            "The combo discount percentage changed. Reload before saving."));
                }

                var effectiveAt = timeProvider.GetUtcNow();
                if (current is not null && effectiveAt <= current.EffectiveFrom)
                {
                    effectiveAt = current.EffectiveFrom.AddMilliseconds(1);
                }

                current?.CloseAt(effectiveAt);
                var policy = SurveyComboDiscountPolicy.Create(
                    command.ExpectedVersion + 1,
                    command.DiscountPercentage,
                    actorId,
                    effectiveAt);
                repository.Add(policy);

                using var oldData = JsonSerializer.SerializeToDocument(new
                {
                    CurrentPolicyId = current?.Id,
                    CurrentDiscountPercentage = current?.DiscountPercentage
                });
                using var newData = JsonSerializer.SerializeToDocument(new
                {
                    policy.Id,
                    policy.VersionNumber,
                    policy.DiscountPercentage,
                    policy.EffectiveFrom
                });
                auditWriter.AddSystemAdminAction(
                    unitOfWork,
                    actorId,
                    executionContext.CorrelationId,
                    nameof(SurveyComboDiscountPolicy),
                    policy.Id,
                    "CREATE_VERSION",
                    oldData,
                    newData,
                    effectiveAt);
                await unitOfWork.SaveChangesAsync(token);

                return Result.Success(new SurveyComboDiscountPolicyResponse(
                    policy.Id,
                    policy.VersionNumber,
                    policy.DiscountPercentage,
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

    private static Result<SurveyComboDiscountPolicyResponse> Conflict() =>
        Result.Failure<SurveyComboDiscountPolicyResponse>(
            AppError.Conflict("SurveyComboDiscount.PersistenceConflict",
                "The combo discount policy changed concurrently."));
}


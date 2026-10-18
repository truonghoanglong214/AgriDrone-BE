using System.Text.Json;
using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.CreateSourcedGuidance;

internal sealed class CreateSourcedGuidanceCommandHandler(
    IPlantConditionRepository conditionRepository,
    ISourcedGuidanceRepository guidanceRepository,
    IPlantsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSourcedGuidanceCommand,
        Result<SourcedGuidanceResponse>>
{
    public async Task<Result<SourcedGuidanceResponse>> Handle(
        CreateSourcedGuidanceCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<SourcedGuidanceResponse>(
                SourcedGuidanceError.Forbidden());
        }

        var condition = await conditionRepository.GetByIdAsync(
            command.PlantConditionId,
            cancellationToken);
        if (condition is null || !condition.IsActive ||
            condition.PlantHealthLabelKind != PlantHealthLabelKind.SuspectedDisease)
        {
            return Result.Failure<SourcedGuidanceResponse>(
                SourcedGuidanceError.InvalidSuspectedClass());
        }

        var now = timeProvider.GetUtcNow();
        var effectiveFrom = command.EffectiveFrom ?? now;
        var versionNumber = await guidanceRepository.GetNextVersionNumberAsync(
            command.Code,
            cancellationToken);

        SourcedGuidance guidance;
        try
        {
            guidance = SourcedGuidance.CreateDraft(
                command.Code,
                versionNumber,
                condition.Id,
                condition.RevisionNumber,
                condition.Code,
                command.SourceTitle,
                command.SourceReference,
                command.GuidanceText,
                effectiveFrom,
                command.EffectiveTo,
                actorId,
                now);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<SourcedGuidanceResponse>(AppError.Validation(
                "SourcedGuidance.InvalidInput", exception.Message));
        }

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            guidance.Code,
            guidance.VersionNumber,
            guidance.PlantConditionId,
            guidance.PlantConditionRevisionNumber,
            guidance.SuspectedClassCode,
            guidance.SourceReference,
            guidance.EffectiveFrom,
            guidance.EffectiveTo,
            guidance.Status
        });

        guidanceRepository.Add(guidance);
        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(SourcedGuidance),
            guidance.Id,
            "CREATE_DRAFT",
            null,
            auditData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.IsUniqueConstraintViolation(
                "uq_sourced_guidance_code_version"))
        {
            return Result.Failure<SourcedGuidanceResponse>(
                SourcedGuidanceError.VersionConflict());
        }

        return Result.Success(Map(guidance));
    }

    internal static SourcedGuidanceResponse Map(SourcedGuidance guidance) => new(
        guidance.Id,
        guidance.Code,
        guidance.VersionNumber,
        guidance.PlantConditionId!.Value,
        guidance.PlantConditionRevisionNumber!.Value,
        guidance.SuspectedClassCode,
        guidance.SourceTitle,
        guidance.SourceReference,
        guidance.GuidanceText,
        guidance.Status,
        guidance.EffectiveFrom,
        guidance.EffectiveTo,
        guidance.Version);
}

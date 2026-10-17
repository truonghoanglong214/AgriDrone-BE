using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServicePrice;

internal sealed class CreateSurveyServicePriceCommandHandler(
    ISurveyServiceRepository repository,
    ISurveyCatalogueQueries catalogueQueries,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        CreateSurveyServicePriceCommand,
        Result<CreateSurveyServicePriceResponse>>
{
    private const string PriceOverlapConstraint =
        "ex_survey_service_prices_no_overlap";

    public async Task<Result<CreateSurveyServicePriceResponse>> Handle(
        CreateSurveyServicePriceCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.CurrentUserRequired());
        }

        var service = await repository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);
        if (service is null)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.NotFound());
        }

        var now = timeProvider.GetUtcNow();

        if (!string.Equals(
                request.Currency?.Trim(),
                "VND",
                StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.UnsupportedCurrency());
        }

        try
        {
            service.EnsureVersion(request.ExpectedServiceVersion);
        }
        catch (SurveyDomainException)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.ConcurrentUpdate());
        }

        if (service.Status == SurveyServiceStatus.Retired)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.InvalidLifecycle(
                    "A retired survey service cannot receive a new price."));
        }

        if (!SurveyServiceMvpPolicy.IsSellable(service.ServiceType))
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.InvalidLifecycle(
                    "Only Plant Health can receive an MVP price version."));
        }

        var priceAtNewStart = service.Prices
            .Where(price => price.PricePerPole.HasValue &&
                            price.IsEffectiveAt(now))
            .ToArray();
        if (priceAtNewStart.Length > 1)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.PriceWindowOverlap());
        }

        var priceToClose = priceAtNewStart.SingleOrDefault();
        if (priceToClose?.EffectiveFrom == now)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.PriceWindowOverlap());
        }

        var hasOtherOverlap = await catalogueQueries
            .HasOverlappingPerPolePriceAsync(
                service.Id,
                now,
                effectiveTo: null,
                priceToClose?.Id,
                cancellationToken);
        if (hasOtherOverlap)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.PriceWindowOverlap());
        }

        var price = SurveyServicePrice.CreatePerPole(
            service.Id,
            PricePerPole.Create(request.AmountPerPole),
            CurrencyCode.Vnd,
            now,
            effectiveTo: null,
            actorId,
            now);

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            CurrentPriceVersionId = priceToClose?.Id,
            CurrentPriceEffectiveFrom = priceToClose?.EffectiveFrom,
            CurrentPriceEffectiveTo = priceToClose?.EffectiveTo
        });

        try
        {
            priceToClose?.CloseAt(now);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                exception.Code == SurveyServiceDomainErrorCodes.VersionConflict
                    ? SurveyServiceError.ConcurrentUpdate()
                    : SurveyServiceError.PriceWindowOverlap());
        }

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            PriceVersionId = price.Id,
            service.Id,
            AmountPerPole = price.PricePerPole!.Value.Amount,
            price.Currency,
            price.EffectiveFrom,
            price.EffectiveTo,
            ClosedPriceVersionId = priceToClose?.Id
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    if (priceToClose is not null)
                    {
                        await unitOfWork.SaveChangesAsync(
                            transactionCancellationToken);
                    }

                    service.AddPriceVersion(
                        price,
                        now,
                        request.ExpectedServiceVersion);
                    // The price already has a generated Guid. Merely attaching it
                    // through the collection makes EF treat the detached child as
                    // Modified, which produces an UPDATE of a row that does not yet
                    // exist and is surfaced as a concurrency conflict.
                    repository.AddPrice(price);
                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyServicePrice",
                        price.Id,
                        "CREATE_VERSION",
                        oldData,
                        newData,
                        now);

                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);
                    return true;
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.ConcurrentUpdate());
        }
        catch (DbUpdateException exception)
            when (exception.IsExclusionConstraintViolation(
                PriceOverlapConstraint))
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.PriceWindowOverlap());
        }
        catch (DbUpdateException exception)
            when (exception.HasPostgresSqlState(
                PostgresErrorCodes.CheckViolation))
        {
            return Result.Failure<CreateSurveyServicePriceResponse>(
                SurveyServiceError.PriceWindowOverlap());
        }

        return Result.Success(new CreateSurveyServicePriceResponse(
            price.Id,
            service.Id,
            price.PricePerPole!.Value.Amount,
            price.Currency,
            price.EffectiveFrom,
            priceToClose?.Id,
            service.Version,
            price.CreatedAt));
    }
}

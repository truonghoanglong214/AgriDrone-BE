using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Application.Policies;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Provisioning;

public sealed record ProvisionFarmRequest(
    Guid TenantId,
    Guid CreatedBy,
    string Code,
    string Name,
    string? Address,
    Polygon? Boundary,
    Point? CenterPoint,
    decimal? AreaHectares);

public sealed record ProvisionedFarm(
    Guid FarmId,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    Polygon? Boundary,
    Point? CenterPoint,
    decimal? AreaHectares,
    GeneralStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

public interface IFarmProvisioningPort
{
    Task<Result<ProvisionedFarm>> ProvisionAsync(
        ProvisionFarmRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class FarmProvisioningPort(
    IFarmRepository farmRepository,
    IFarmUnitOfWork unitOfWork,
    IFarmGeometryPolicy geometryPolicy,
    TimeProvider timeProvider) : IFarmProvisioningPort
{
    private const string ActiveFarmCodeConstraint = "ux_farms_tenant_code_active";

    public async Task<Result<ProvisionedFarm>> ProvisionAsync(
        ProvisionFarmRequest request,
        CancellationToken cancellationToken = default)
    {
        var geometryDecision = geometryPolicy.ValidateFarm(
            request.Boundary,
            request.CenterPoint,
            request.AreaHectares);
        if (geometryDecision.IsFailure)
        {
            return Result.Failure<ProvisionedFarm>(geometryDecision.Error);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var existing = await farmRepository.GetByCodeAsync(
            request.TenantId,
            code,
            cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<ProvisionedFarm>(FarmError.CodeAlreadyExists(code));
        }

        var farm = Farm.Create(
            request.TenantId,
            code,
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            request.Boundary,
            request.CenterPoint,
            request.AreaHectares,
            GeneralStatus.Active,
            request.CreatedBy,
            timeProvider.GetUtcNow());
        farmRepository.Add(farm);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(ActiveFarmCodeConstraint))
        {
            return Result.Failure<ProvisionedFarm>(FarmError.CodeAlreadyExists(code));
        }

        return Result.Success(new ProvisionedFarm(
            farm.Id,
            farm.TenantId,
            farm.Code,
            farm.Name,
            farm.Address,
            farm.Boundary,
            farm.CenterPoint,
            farm.AreaHectares,
            farm.Status,
            farm.CreatedBy,
            farm.CreatedAt));
    }
}

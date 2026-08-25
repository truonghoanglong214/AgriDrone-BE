using AgriDrone.Api.Contracts.Drones;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
public sealed class DronesController(
    ISender sender,
    IAuthorizationService authorizationService,
    ICurrentTenant currentTenant) : ControllerBase
{
    [HttpPost("api/tenants/{tenantId:guid}/drones")]
    [Authorize(
        Policy = IdentityAuthorizationPolicies.SystemAdmin)]
    public async Task<IResult> RegisterDrone(
        Guid tenantId,
        [FromBody] RegisterDroneRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterDroneCommand(
            TenantId: tenantId,
            Code: request.Code,
            Name: request.Name,
            Model: request.Model,
            Manufacturer: request.Manufacturer,
            Specifications: request.Specifications,
            SerialNumber: request.SerialNumber,
            RegistrationNumber: request.RegistrationNumber,
            RegistrationDate: request.RegistrationDate,
            RegistrationExpiryDate: request.RegistrationExpiryDate,
            WeightKg: request.WeightKg,
            Notes: request.Notes);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drone => Results.Created(
                $"/api/tenants/{tenantId}/drones/{drone.Id}",
                drone));
    }

    [HttpPatch(
        "api/tenants/{tenantId:guid}/drones/{droneId:guid}/status")]
    [Authorize(
        Policy = IdentityAuthorizationPolicies.SystemAdmin)]
    public async Task<IResult> ChangeStatus(
        Guid tenantId,
        Guid droneId,
        [FromBody] ChangeDroneStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangeDroneStatusCommand(
            tenantId,
            droneId,
            request.Status,
            request.NextMaintenanceAt);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response));
    }

    [HttpGet(
        "api/farms/{farmId:guid}/drones/available")]
    [Authorize]
    public async Task<IResult> GetAvailableDrones(
        Guid farmId,
        [FromQuery] GetAvailableDronesRequest request,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var authorizationResult =
            await authorizationService.AuthorizeAsync(
                User,
                new FarmAccessTarget(
                    tenantId,
                    farmId),
                IdentityAuthorizationPolicies.FarmManager);

        if (!authorizationResult.Succeeded)
        {
            return Results.Forbid();
        }

        var query = new GetAvailableDronesQuery(
            request.StartAt,
            request.EndAt);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drones => Results.Ok(drones));
    }
}
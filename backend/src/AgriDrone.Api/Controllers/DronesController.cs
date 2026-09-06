using AgriDrone.Api.Contracts.Drones;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;
using AgriDrone.SharedInfrastructure.Authorization;
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
    /// <summary>Đăng ký drone cho tenant.</summary>
    /// <remarks>
    /// System Admin tạo hồ sơ drone với mã, model, thông số kỹ thuật và thông tin
    /// đăng ký. Mã drone và serial number phải đáp ứng quy tắc duy nhất.
    /// </remarks>
    [HttpPost("api/tenants/{tenantId:guid}/drones")]
    [Authorize(
        Policy = AccessAuthorizationPolicies.SystemAdmin)]
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

    /// <summary>Thay đổi trạng thái drone.</summary>
    /// <remarks>
    /// System Admin chuyển drone giữa các trạng thái vận hành như Available,
    /// Maintenance hoặc Retired theo state transition được hỗ trợ. Khi hoàn tất
    /// bảo trì, thời điểm bảo trì kế tiếp phải nằm trong tương lai.
    /// </remarks>
    [HttpPatch(
        "api/tenants/{tenantId:guid}/drones/{droneId:guid}/status")]
    [Authorize(
        Policy = AccessAuthorizationPolicies.SystemAdmin)]
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

    /// <summary>Tìm drone khả dụng cho khoảng thời gian Mission.</summary>
    /// <remarks>
    /// Farm Manager truy vấn các drone đang Available trong tenant và không có
    /// Mission giao lịch với khoảng thời gian yêu cầu.
    /// </remarks>
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
                AccessAuthorizationPolicies.FarmManage);

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

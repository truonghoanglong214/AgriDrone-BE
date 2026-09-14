using AgriDrone.Api.Contracts.Drones;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneDetails;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDrones;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application
    .Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/drones")]
public sealed class DronesController(
    ISender sender,
    IAuthorizationService authorizationService,
    IExecutionContext executionContext)
    : ControllerBase
{
    /// <summary>Đăng ký Drone cho tenant hiện tại.</summary>
    /// <remarks>
    /// Tenant Admin tạo Drone thuộc tenant lấy từ JWT. Mã Drone và serial number
    /// phải đáp ứng quy tắc duy nhất; client không được tự truyền TenantId.
    /// Drone mới được khởi tạo theo trạng thái vận hành mặc định của domain.
    /// </remarks>
    [HttpPost]
    [Authorize(
        Policy = AccessAuthorizationPolicies.TenantAdmin)]
    public async Task<IResult> RegisterDrone(
        [FromBody] RegisterDroneRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new RegisterDroneCommand(
            TenantId: tenantId,
            Code: request.Code,
            Name: request.Name,
            Model: request.Model,
            Manufacturer: request.Manufacturer,
            Specifications: request.Specifications,
            SerialNumber: request.SerialNumber,
            RegistrationNumber:
                request.RegistrationNumber,
            RegistrationDate:
                request.RegistrationDate,
            RegistrationExpiryDate:
                request.RegistrationExpiryDate,
            WeightKg: request.WeightKg,
            Notes: request.Notes);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drone => Results.Created(
                $"/api/drones/{drone.Id}",
                drone));
    }

    /// <summary>Lấy danh sách Drone của tenant hiện tại.</summary>
    /// <remarks>
    /// Tenant Admin truy vấn danh sách Drone phân trang trong tenant lấy từ JWT.
    /// Có thể lọc theo trạng thái và tìm kiếm theo thông tin Drone; dữ liệu của
    /// tenant khác không được trả về.
    /// </remarks>
    [HttpGet]
    [Authorize(
        Policy = AccessAuthorizationPolicies.TenantAdmin)]
    public async Task<IResult> GetDrones(
        [FromQuery] GetDronesRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var query = new GetDronesQuery(
            TenantId: tenantId,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            Status: request.Status,
            Search: request.Search);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drones => Results.Ok(drones));
    }

    /// <summary>Lấy chi tiết một Drone của tenant hiện tại.</summary>
    /// <remarks>
    /// Tenant Admin xem thông tin kỹ thuật, đăng ký, trạng thái và lịch bảo trì
    /// của Drone. Yêu cầu chỉ thành công khi Drone thuộc tenant lấy từ JWT.
    /// </remarks>
    [HttpGet("{droneId:guid}")]
    [Authorize(
        Policy = AccessAuthorizationPolicies.TenantAdmin)]
    public async Task<IResult> GetDroneDetails(
        Guid droneId,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var query = new GetDroneDetailsQuery(
            TenantId: tenantId,
            DroneId: droneId);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drone => Results.Ok(drone));
    }

    /// <summary>Thay đổi trạng thái vận hành của Drone.</summary>
    /// <remarks>
    /// Tenant Admin chuyển Drone của tenant mình giữa các trạng thái được domain
    /// cho phép, như Available, Maintenance, Inactive hoặc Retired. Backend từ
    /// chối transition không hợp lệ hoặc làm Drone không khả dụng khi còn Mission
    /// đang chặn. Expected tenant được lấy từ JWT, không lấy từ request.
    /// </remarks>
    [HttpPatch(
        "{droneId:guid}/operational-status")]
    [Authorize(
        Policy = AccessAuthorizationPolicies.TenantAdmin)]
    public async Task<IResult> ChangeOperationalStatus(
        Guid droneId,
        [FromBody] ChangeDroneStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new ChangeDroneStatusCommand(
            TenantId: tenantId,
            DroneId: droneId,
            TargetStatus: request.Status,
            NextMaintenanceAt:
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
        "/api/farms/{farmId:guid}/drones/available")]
    [Authorize]
    public async Task<IResult> GetAvailableDrones(
        Guid farmId,
        [FromQuery] GetAvailableDronesRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
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

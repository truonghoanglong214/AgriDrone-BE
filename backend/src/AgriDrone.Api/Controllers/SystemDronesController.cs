using AgriDrone.Api.Contracts.Drones;
using AgriDrone.Modules.Missions.Application.Features.Drones.ChangeDroneStatus;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneMaintenanceHistory;
using AgriDrone.Modules.Missions.Application.Features.Drones.RegisterDrone;
using AgriDrone.Modules.Missions.Application.Features.Drones.UpdateDrone;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/drones")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemDronesController(ISender sender) : ControllerBase
{
    /// <remarks>Chỉ SystemAdmin xem danh sách drone cấp hệ thống. Registry nội bộ không dành cho TenantOwner.</remarks>
    [HttpGet]
    public async Task<IResult> GetRegistry(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetDroneRegistryQuery(),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Xem lịch sử bảo trì của drone để đối chiếu tình trạng vận hành và các sự cố đã ghi nhận.</remarks>
    [HttpGet("{droneId:guid}/maintenance-history")]
    public async Task<IResult> GetMaintenanceHistory(
        [FromRoute] Guid droneId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetDroneMaintenanceHistoryQuery(droneId), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Đăng ký drone cấp hệ thống cùng thông số, serial và thông tin đăng ký. Code, serial và registration phải duy nhất.</remarks>
    [HttpPost]
    public async Task<IResult> Register(
        [FromBody] RegisterDroneRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterDroneCommand(
                request.Code,
                request.Name,
                request.Model,
                request.Manufacturer,
                request.Specifications,
                request.SerialNumber,
                request.RegistrationNumber,
                request.RegistrationDate,
                request.RegistrationExpiryDate,
                request.WeightKg,
                request.Notes),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            drone => Results.Created($"/api/system/drones/{drone.Id}", drone));
    }

    /// <remarks>Cập nhật thông tin drone. ExpectedVersion dùng để phát hiện cập nhật đồng thời.</remarks>
    [HttpPut("{droneId:guid}")]
    public async Task<IResult> Update(
        [FromRoute] Guid droneId,
        [FromBody] UpdateDroneRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateDroneCommand(
                droneId,
                request.ExpectedVersion,
                request.Name,
                request.Model,
                request.Manufacturer,
                request.Specifications,
                request.SerialNumber,
                request.RegistrationNumber,
                request.RegistrationDate,
                request.RegistrationExpiryDate,
                request.WeightKg,
                request.Notes),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Đổi trạng thái vận hành hoặc bảo trì của drone, kèm lý do và lịch bảo trì kế tiếp khi áp dụng. Trạng thái mới ảnh hưởng đến khả năng được chọn cho mission.</remarks>
    [HttpPatch("{droneId:guid}/status")]
    public async Task<IResult> ChangeStatus(
        [FromRoute] Guid droneId,
        [FromBody] ChangeDroneStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ChangeDroneStatusCommand(
                droneId,
                request.Status,
                request.NextMaintenanceAt,
                request.ExpectedVersion,
                request.Reason),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}

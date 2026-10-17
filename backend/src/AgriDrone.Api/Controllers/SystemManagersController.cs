using AgriDrone.Api.Contracts.SystemManagers;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfile;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfiles;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/managers")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemManagersController(ISender sender) : ControllerBase
{
    /// <summary>Lấy danh sách hồ sơ System Manager và version hiện tại.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<SystemManagerProfileApiResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IResult> GetProfiles(
        [FromQuery] GetSystemManagersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSystemManagerProfilesQuery(
                MapStatus(request.Status),
                MapAvailability(request.Availability),
                MapQualificationStatus(request.QualificationStatus),
                request.PageNumber,
                request.PageSize),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            page => Results.Ok(
                SystemManagerProfileResponseMapper.ToResponse(page)));
    }

    /// <summary>Lấy chi tiết hồ sơ System Manager và version hiện tại.</summary>
    [HttpGet("{profileId:guid}")]
    [ProducesResponseType<SystemManagerProfileApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetProfile(
        [FromRoute] Guid profileId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSystemManagerProfileQuery(profileId),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SystemManagerProfileResponseMapper.ToResponse(response)));
    }

    [HttpPost]
    public async Task<IResult> Create(
        [FromBody] CreateSystemManagerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSystemManagerProfileCommand(request.UserId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                SystemManagerProfileResponseMapper.ToResponse(response),
                statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Kích hoạt hồ sơ System Manager.</summary>
    /// <remarks>
    /// Chuyển trạng thái hồ sơ sang Active. Thao tác này không tự động đánh dấu
    /// manager là Available và không thay đổi tình trạng chứng chỉ bay. Gửi version
    /// mới nhất từ API GET chi tiết để chống cập nhật đồng thời; reason được lưu audit.
    /// </remarks>
    [HttpPut("{profileId:guid}/activate")]
    [ProducesResponseType<SystemManagerProfileApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Activate(
        [FromRoute] Guid profileId,
        [FromBody] SystemManagerStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ActivateSystemManagerProfileCommand(
                profileId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SystemManagerProfileResponseMapper.ToResponse(response)));
    }

    /// <summary>Đình chỉ hồ sơ System Manager.</summary>
    /// <remarks>
    /// Chuyển trạng thái hồ sơ sang Suspended và đồng thời đặt availability thành
    /// Unavailable, ngăn manager nhận phân công hoặc vận hành mới. Reason được lưu audit.
    /// </remarks>
    [HttpPut("{profileId:guid}/suspend")]
    [ProducesResponseType<SystemManagerProfileApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Suspend(
        [FromRoute] Guid profileId,
        [FromBody] SystemManagerStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SuspendSystemManagerProfileCommand(
                profileId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SystemManagerProfileResponseMapper.ToResponse(response)));
    }

    /// <summary>Cập nhật khả năng nhận việc của System Manager.</summary>
    /// <remarks>
    /// Đặt manager thành Available hoặc Unavailable. Manager chỉ thực sự đủ điều kiện
    /// nhận phân công khi hồ sơ Active, availability là Available và chứng chỉ bay
    /// đang Qualified, chưa hết hạn.
    /// </remarks>
    [HttpPut("{profileId:guid}/availability")]
    [ProducesResponseType<SystemManagerProfileApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> UpdateAvailability(
        [FromRoute] Guid profileId,
        [FromBody] UpdateSystemManagerAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateSystemManagerAvailabilityCommand(
                profileId,
                MapAvailability(request.Availability),
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SystemManagerProfileResponseMapper.ToResponse(response)));
    }

    /// <summary>Cập nhật tình trạng chứng chỉ bay của System Manager.</summary>
    /// <remarks>
    /// Cập nhật Pending, Qualified, Suspended hoặc Revoked. Trạng thái Qualified yêu
    /// cầu expiresAt ở tương lai; mọi trạng thái khác Qualified sẽ tự đặt manager về
    /// Unavailable.
    /// </remarks>
    [HttpPut("{profileId:guid}/qualification")]
    [ProducesResponseType<SystemManagerProfileApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> UpdateQualification(
        [FromRoute] Guid profileId,
        [FromBody] UpdateSystemManagerQualificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateSystemManagerQualificationCommand(
                profileId,
                MapQualificationStatus(request.Status),
                request.ExpiresAt,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SystemManagerProfileResponseMapper.ToResponse(response)));
    }

    [HttpPost("invitations")]
    public async Task<IResult> Invite(
        [FromBody] InviteSystemManagerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new InviteSystemManagerCommand(
                request.Email),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                response,
                statusCode: StatusCodes.Status201Created));
    }

    private static SystemManagerProfileStatus? MapStatus(
        SystemManagerProfileStatusValue? status) =>
        status switch
        {
            null => null,
            SystemManagerProfileStatusValue.ACTIVE =>
                SystemManagerProfileStatus.Active,
            SystemManagerProfileStatusValue.SUSPENDED =>
                SystemManagerProfileStatus.Suspended,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    private static SystemManagerAvailabilityStatus? MapAvailability(
        SystemManagerAvailabilityValue? availability) =>
        availability switch
        {
            null => null,
            SystemManagerAvailabilityValue.AVAILABLE =>
                SystemManagerAvailabilityStatus.Available,
            SystemManagerAvailabilityValue.UNAVAILABLE =>
                SystemManagerAvailabilityStatus.Unavailable,
            _ => throw new ArgumentOutOfRangeException(
                nameof(availability),
                availability,
                null)
        };

    private static SystemManagerAvailabilityStatus MapAvailability(
        SystemManagerAvailabilityValue availability) =>
        MapAvailability((SystemManagerAvailabilityValue?)availability)!.Value;

    private static FlightQualificationStatus? MapQualificationStatus(
        FlightQualificationStatusValue? status) =>
        status switch
        {
            null => null,
            FlightQualificationStatusValue.PENDING =>
                FlightQualificationStatus.Pending,
            FlightQualificationStatusValue.QUALIFIED =>
                FlightQualificationStatus.Qualified,
            FlightQualificationStatusValue.SUSPENDED =>
                FlightQualificationStatus.Suspended,
            FlightQualificationStatusValue.REVOKED =>
                FlightQualificationStatus.Revoked,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    private static FlightQualificationStatus MapQualificationStatus(
        FlightQualificationStatusValue status) =>
        MapQualificationStatus((FlightQualificationStatusValue?)status)!.Value;
}

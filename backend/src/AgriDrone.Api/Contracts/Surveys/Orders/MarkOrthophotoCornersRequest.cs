using System.ComponentModel.DataAnnotations;
using AgriDrone.Modules.Surveys.Application.Features.Orders.MarkFarmBoundaryOnOrthophoto;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record MarkOrthophotoCornersRequest(
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint ExpectedOrderVersion,

    [param: Required]
    IReadOnlyList<CornerPointDto> Corners,

    string? Reason = null,
    string? EvidenceReference = null);

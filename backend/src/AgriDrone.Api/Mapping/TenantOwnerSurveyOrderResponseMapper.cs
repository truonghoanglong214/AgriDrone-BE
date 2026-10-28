using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Api.Mapping;

internal static class TenantOwnerSurveyOrderResponseMapper
{
    public static TenantOwnerSurveyOrderApiResponse ToResponse(
        TenantOwnerSurveyOrderItem item) => new(
        item.SurveyOrderId,
        item.OrderNumber,
        item.FarmId,
        item.FarmName,
        ToSnakeCaseUpper(item.Status.ToString()),
        item.OrderVersion,
        item.DepositAmount,
        item.TotalAmount,
        item.RemainingAmount,
        item.Currency,
        item.CurrentAppointment is null
            ? null
            : new TenantOwnerAppointmentApiResponse(
                item.CurrentAppointment.AppointmentId,
                item.CurrentAppointment.Purpose ==
                    AgriDrone.Modules.Surveys.Domain.SurveyAppointmentPurpose.BaselineMapping
                    ? "BASELINE_MAPPING"
                    : "PAID_SERVICE",
                item.CurrentAppointment.StartAt,
                item.CurrentAppointment.EndAt,
                ToSnakeCaseUpper(item.CurrentAppointment.Status.ToString()),
                item.CurrentAppointment.AppointmentVersion));

    public static PagedResult<TenantOwnerSurveyOrderApiResponse> ToResponse(
        PagedResult<TenantOwnerSurveyOrderItem> page) => new(
        page.Items.Select(ToResponse).ToArray(),
        page.PageNumber,
        page.PageSize,
        page.TotalCount);

    private static string ToSnakeCaseUpper(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            if (index > 0 && char.IsUpper(value[index]))
                builder.Append('_');
            builder.Append(char.ToUpperInvariant(value[index]));
        }

        return builder.ToString();
    }
}

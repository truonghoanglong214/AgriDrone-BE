using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
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
            : MapCurrentAppointment(item.CurrentAppointment),
        ToSnakeCaseUpper(item.ServiceType.ToString()));

    public static PagedResult<TenantOwnerSurveyOrderApiResponse> ToResponse(
        PagedResult<TenantOwnerSurveyOrderItem> page) => new(
        page.Items.Select(ToResponse).ToArray(),
        page.PageNumber,
        page.PageSize,
        page.TotalCount);

    private static TenantOwnerAppointmentApiResponse MapCurrentAppointment(
        TenantOwnerCurrentAppointmentItem item)
    {
        var (singleDate, startDate, endDate) = SurveyAppointmentWindow.ToLocalDates(
            item.StartAt,
            item.EndAt);

        return new TenantOwnerAppointmentApiResponse(
            item.AppointmentId,
            item.Purpose == SurveyAppointmentPurpose.BaselineMapping
                ? "BASELINE_MAPPING"
                : "PAID_SERVICE",
            item.StartAt,
            item.EndAt,
            ToSnakeCaseUpper(item.Status.ToString()),
            item.AppointmentVersion,
            singleDate,
            startDate,
            endDate);
    }

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

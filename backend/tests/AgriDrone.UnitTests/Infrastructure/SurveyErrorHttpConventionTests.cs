using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure;

public sealed class SurveyErrorHttpConventionTests
{
    [Theory]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Validation, StatusCodes.Status422UnprocessableEntity)]
    public void ApplicationErrorsMapToLockedHttpStatuses(
        ErrorType errorType,
        int expectedStatus)
    {
        var error = new AppError("Survey.Test", "test", errorType);
        var result = Result.Failure(error).ToHttpResult(
            new DefaultHttpContext(),
            Results.NoContent);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(expectedStatus, statusResult.StatusCode);
    }
}

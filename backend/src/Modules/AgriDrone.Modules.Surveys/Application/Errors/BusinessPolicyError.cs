using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class BusinessPolicyError
{
    public static AppError NotFound(string entityName = "Business policy") =>
        AppError.NotFound(
            "BusinessPolicy.NotFound",
            $"{entityName} was not found.");

    public static AppError CurrentUserRequired() =>
        AppError.Unauthorized(
            "BusinessPolicy.CurrentUserRequired",
            "An authenticated system administrator is required.");

    public static AppError ConcurrentUpdate(
        string message = "The policy configuration changed in another request. Reload it and try again.") =>
        AppError.Conflict(
            "BusinessPolicy.ConcurrentUpdate",
            message);

    public static AppError InvalidLifecycle(string description) =>
        AppError.Conflict(
            "BusinessPolicy.InvalidLifecycle",
            description);

    public static AppError PredecessorMismatch(int expected, int actual) =>
        AppError.Conflict(
            "BusinessPolicy.PredecessorMismatch",
            $"Expected policy predecessor version {expected}, but found {actual}.");

    public static AppError DecisionMismatch(Guid expected, Guid actual) =>
        AppError.Conflict(
            "BusinessPolicy.DecisionMismatch",
            $"Expected activation decision {expected}, but found {actual}.");

    public static AppError InactiveDependency(string dependencyName) =>
        AppError.Conflict(
            "BusinessPolicy.InactiveDependency",
            $"The referenced {dependencyName} is not currently effective or not found.");

    public static AppError UnsupportedCurrency(string currency) =>
        AppError.Validation(
            "BusinessPolicy.UnsupportedCurrency",
            $"Only VND currency is supported for V3 policies, but got '{currency}'.");

    public static AppError InvalidAmount(string description) =>
        AppError.Validation(
            "BusinessPolicy.InvalidAmount",
            description);

    public static AppError PublicIntakeDisabled() =>
        AppError.Conflict(
            "BusinessPolicy.PublicIntakeDisabled",
            "Public commercial intake is currently disabled by system policy activation.");

    public static AppError DuplicateServiceCode(string code) =>
        AppError.Conflict(
            "BusinessPolicy.DuplicateServiceCode",
            $"A survey service with code '{code}' already exists.");
}

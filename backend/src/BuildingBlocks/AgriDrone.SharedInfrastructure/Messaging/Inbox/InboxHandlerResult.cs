namespace AgriDrone.SharedInfrastructure.Messaging.Inbox;

public sealed record InboxHandlerResult(
    InboxHandlerDisposition Disposition,
    string? Result = null,
    string? ErrorCode = null,
    string? Error = null)
{
    public static InboxHandlerResult Completed(string? result = null) =>
        new(InboxHandlerDisposition.Completed, Result: result);

    public static InboxHandlerResult Retry(string? error = null) =>
        new(InboxHandlerDisposition.Retry, Error: error);

    public static InboxHandlerResult PermanentFailure(
        string errorCode,
        string? error = null) =>
        new(
            InboxHandlerDisposition.PermanentFailure,
            ErrorCode: errorCode,
            Error: error);
}

using AgriDrone.Api.Contracts.Messaging;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.SharedInfrastructure.Messaging.Recovery;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/messaging")]
[Authorize(Policy = IdentityAuthorizationPolicies.SystemAdmin)]
public sealed class MessagingOperationsController(
    IMessagingRecoveryService recoveryService,
    ICurrentUser currentUser,
    IExecutionContext executionContext) : ControllerBase
{
    [HttpPost("outbox/{messageId:guid}/redrive")]
    public async Task<IResult> RedriveOutbox(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var actorId = RequireActor();
        var redriven = await recoveryService.RedriveOutboxAsync(
            messageId,
            actorId,
            executionContext.CorrelationId,
            cancellationToken);
        return redriven
            ? Results.Ok(new { MessageId = messageId, Redriven = true })
            : Results.NotFound();
    }

    [HttpPost("dead-letters/{consumerName}/redrive")]
    public async Task<IResult> RedriveDeadLetters(
        string consumerName,
        RedriveDeadLettersRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MaximumMessages is < 1 or > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.MaximumMessages)] =
                    ["MaximumMessages must be between 1 and 100."]
            });
        }

        var count = await recoveryService.RedriveDeadLettersAsync(
            consumerName,
            request.MaximumMessages,
            RequireActor(),
            executionContext.CorrelationId,
            cancellationToken);
        return Results.Ok(new { ConsumerName = consumerName, Redriven = count });
    }

    private Guid RequireActor() =>
        currentUser.UserId is Guid actorId && actorId != Guid.Empty
            ? actorId
            : throw new InvalidOperationException(
                "A System Admin actor is required for messaging recovery.");
}

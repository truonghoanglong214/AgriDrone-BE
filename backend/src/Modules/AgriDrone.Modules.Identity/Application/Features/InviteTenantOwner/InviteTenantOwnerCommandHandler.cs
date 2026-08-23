using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.InviteTenantAdmin;
using AgriDrone.Modules.Identity.Application.Invitations.EmailDelivery;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantOwner
{
    internal sealed class InviteTenantOwnerCommandHandler(
    IdentityDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator,
    ITenantInvitationEmailDelivery emailDelivery) : IIntegrationMessageHandler<TenantInvitationEmailRequestedV1>
    {
        public Task<IntegrationMessageProcessingResult> HandleAsync(IntegrationEventEnvelope<TenantInvitationEmailRequestedV1> envelope, CancellationToken cancellationToken)
        {
            return inboxCoordinator.ExecuteAsync(
                dbContext,
                IntegrationConsumerNames.EmailTenantInvitationV1,
                envelope,
                (_, token) => DeliverAsync(envelope, token),
                cancellationToken);
        }

        private async Task<InboxHandlerResult> DeliverAsync(
            IntegrationEventEnvelope<TenantInvitationEmailRequestedV1> envelope,
            CancellationToken cancellationToken)
        {
            var result = await emailDelivery.DeliverAsync(
                envelope.TenantId,
                envelope.Payload.InvitationId,
                envelope.Payload.PlainTextToken,
                cancellationToken);

            if (result.IsFailure)
            {
                return InboxHandlerResult.PermanentFailure(
                    result.Error.Code,
                    result.Error.Description);
            }

            return InboxHandlerResult.Completed(
                JsonSerializer.Serialize(result.Value));
        }
    }
}

using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Identity.Application.Abstractions.Messaging;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Application.Features.RegisterUser;

internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    ITenantRepository tenantRepository,
    ITenantMembershipRepository tenantMembershipRepository,
    IIdentityIntegrationOutbox integrationOutbox,
    IExecutionContext executionContext,
    IOptions<TenantRegistrationOptions> registrationOptions,
    TimeProvider timeProvider,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<RegisterUserCommand, Result<RegisterUserResponse>>
{
    private readonly TenantRegistrationOptions _registrationOptions =
        registrationOptions.Value;

    public Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => RegisterAsync(
                request,
                transactionCancellationToken),
            cancellationToken);

    private async Task<Result<RegisterUserResponse>> RegisterAsync(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var tenantCode = request.TenantCode.Trim().ToUpperInvariant();
        var tenantName = request.TenantName.Trim();
        var fullName = request.FullName.Trim();
        var phone = NormalizePhone(request.Phone);

        if (await userRepository.GetByEmailAsync(
                email,
                cancellationToken) is not null)
        {
            return Result.Failure<RegisterUserResponse>(
                UserError.EmailAlreadyExists(email));
        }

        if (await tenantRepository.GetByCodeAsync(
                tenantCode,
                cancellationToken) is not null)
        {
            return Result.Failure<RegisterUserResponse>(
                TenantError.CodeAlreadyExists(tenantCode));
        }

        var now = timeProvider.GetUtcNow();
        var user = User.Create(
            email,
            passwordService.HashPassword(request.Password),
            fullName,
            phone,
            UserStatus.Active,
            now);
        var tenant = Tenant.Create(
            tenantCode,
            tenantName,
            GeneralStatus.Active,
            now);
        var membership = TenantMembership.Create(
            tenant.Id,
            user.Id,
            TenantMemberRole.Owner,
            GeneralStatus.Active,
            joinedAt: now,
            createAt: now);

        userRepository.Add(user);
        tenantRepository.Add(tenant);
        tenantMembershipRepository.Add(membership);

        AddRegistrationSuccessEmail(user, tenant, membership.Role, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new RegisterUserResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Phone,
                tenant.Code,
                tenant.Name,
                user.CreatedAt));
    }

    private void AddRegistrationSuccessEmail(
        User user,
        Tenant tenant,
        TenantMemberRole role,
        DateTimeOffset registeredAt)
    {
        var notificationId = Guid.NewGuid();
        var payload = new EmailNotificationRequestedV1(
            NotificationId: notificationId,
            TemplateKey: EmailTemplateKeys.TenantRegistrationSuccess,
            Recipients:
            [
                new EmailRecipientV1(user.Email, user.FullName)
            ],
            Variables: new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.UserName] = user.FullName,
                [EmailTemplateVariableKeys.TenantName] = tenant.Name,
                [EmailTemplateVariableKeys.TenantCode] = tenant.Code,
                [EmailTemplateVariableKeys.RoleName] =
                    GetRoleDisplayName(role),
                [EmailTemplateVariableKeys.RegisteredAt] =
                    registeredAt.ToString("O"),
                [EmailTemplateVariableKeys.LoginUrl] =
                    _registrationOptions.LoginUrl
            });
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.EmailNotificationRequestedV1,
            messageId: Guid.NewGuid(),
            correlationId: executionContext.CorrelationId,
            tenantId: tenant.Id,
            actorId: user.Id,
            occurredAt: registeredAt,
            payload);

        integrationOutbox.Add(
            envelope,
            partitionKey: notificationId.ToString("D"));
    }

    private static string? NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

    private static string GetRoleDisplayName(TenantMemberRole role) =>
        role switch
        {
            TenantMemberRole.Owner => "Tenant Owner",
            TenantMemberRole.TenantAdmin => "Tenant Admin",
            TenantMemberRole.Member => "Tenant Member",
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Unsupported tenant role.")
        };
}

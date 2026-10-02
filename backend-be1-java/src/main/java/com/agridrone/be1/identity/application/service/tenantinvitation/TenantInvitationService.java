package com.agridrone.be1.identity.application.service.tenantinvitation;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.error.TenantInvitationErrorCodes;
import com.agridrone.be1.identity.application.port.in.tenantinvitation.TenantInvitationUseCase;
import com.agridrone.be1.identity.application.port.out.notification.TenantInvitationPolicy;
import com.agridrone.be1.identity.application.port.out.persistence.TenantInvitationRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.InvitationTokenService;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantInvitation;
import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.agridrone.be1.shared.messaging.IntegrationEventEnvelope;
import com.agridrone.be1.shared.messaging.OutboxService;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import java.time.Clock;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.Locale;
import java.util.Objects;
import java.util.UUID;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.http.HttpStatus;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionTemplate;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class TenantInvitationService implements TenantInvitationUseCase {
    static final String INVITATION_EMAIL_EVENT =
            "identity.tenant-invitation-email-requested.v1";
    static final String WELCOME_EMAIL_EVENT = "notification.email-requested.v1";

    private final UserRepository users;
    private final TenantRepository tenants;
    private final TenantMembershipRepository memberships;
    private final TenantInvitationRepository invitations;
    private final InvitationTokenService tokens;
    private final PasswordHasher passwordHasher;
    private final TenantInvitationPolicy policy;
    private final OutboxService outbox;
    private final ObjectMapper objectMapper;
    private final Clock clock;
    private final TransactionTemplate transactions;

    public TenantInvitationService(
            UserRepository users,
            TenantRepository tenants,
            TenantMembershipRepository memberships,
            TenantInvitationRepository invitations,
            InvitationTokenService tokens,
            PasswordHasher passwordHasher,
            TenantInvitationPolicy policy,
            OutboxService outbox,
            ObjectMapper objectMapper,
            Clock clock,
            TransactionTemplate transactions) {
        this.users = users;
        this.tenants = tenants;
        this.memberships = memberships;
        this.invitations = invitations;
        this.tokens = tokens;
        this.passwordHasher = passwordHasher;
        this.policy = policy;
        this.outbox = outbox;
        this.objectMapper = objectMapper;
        this.clock = clock;
        this.transactions = transactions;
    }

    @Override
    public ProvisionResult provisionOwner(UUID tenantId, String rawEmail) {
        try {
            return Objects.requireNonNull(transactions.execute(
                    status -> provisionOwnerInTransaction(tenantId, rawEmail)));
        } catch (DataIntegrityViolationException exception) {
            if (hasConstraint(exception, "uq_tenant_invitations_pending_owner_provisioning")
                    || hasConstraint(exception, "uq_tenant_invitations_pending_tenant_email")) {
                throw ownerProvisioningPending();
            }
            throw exception;
        }
    }

    private ProvisionResult provisionOwnerInTransaction(UUID tenantId, String rawEmail) {
        var context = ExecutionContext.require();
        UUID actorId = context.actorId();
        if (actorId == null) {
            throw new StableApiException(
                    HttpStatus.UNAUTHORIZED,
                    AuthenticationErrorCodes.CURRENT_USER_REQUIRED,
                    "A current user is required.");
        }
        if (!context.roles().contains(SystemRoleCodes.SYSTEM_ADMIN)) {
            throw tenantAccessDenied();
        }

        Tenant tenant = requireActiveTenant(tenantId);
        if (memberships.hasActiveOwner(tenant.id())) {
            throw ownerAlreadyAssigned();
        }

        String email = rawEmail.trim().toLowerCase(Locale.ROOT);
        User existingUser = users.findByEmail(email).orElse(null);
        if (existingUser != null && existingUser.id().equals(actorId)) {
            throw error(
                    HttpStatus.UNPROCESSABLE_ENTITY,
                    TenantInvitationErrorCodes.INVITE_SELF_NOT_ALLOWED,
                    "You cannot invite yourself to the current tenant.");
        }
        if (existingUser != null
                && memberships.find(existingUser.id(), tenant.id()).isPresent()) {
            throw userAlreadyMember();
        }

        Instant now = clock.instant();
        var pending = invitations.findPendingOwnerProvisioning(tenant.id()).orElse(null);
        if (pending != null) {
            if (pending.canAccept(now)) {
                throw ownerProvisioningPending();
            }
            invitations.save(pending.expire(now));
        }

        var generated = tokens.generate();
        TenantInvitation invitation = TenantInvitation.createOwnerProvisioning(
                tenant.id(),
                email,
                generated.tokenHash(),
                actorId,
                now.plus(policy.expiration()),
                now);
        invitations.add(invitation);
        ObjectNode payload = objectMapper.createObjectNode()
                .put("invitationId", invitation.id().toString())
                .put("plainTextToken", generated.plainTextToken());
        enqueue(
                INVITATION_EMAIL_EVENT,
                tenant.id(),
                actorId,
                invitation.id().toString(),
                payload,
                now);
        return new ProvisionResult(invitation.id(), invitation.email(), invitation.expiresAt());
    }

    @Override
    @Transactional(readOnly = true)
    public PreviewResult preview(String plainTextToken) {
        TenantInvitation invitation = invitations.findByTokenHash(tokens.hash(plainTextToken))
                .filter(value -> isUsableOwnerProvisioning(value, clock.instant()))
                .orElseThrow(TenantInvitationService::invalidOrExpired);
        Tenant tenant = requireActiveTenant(invitation.tenantId());
        User user = users.findByEmail(invitation.email()).orElse(null);
        if (user != null && !user.isActive()) {
            throw userInactive();
        }
        return new PreviewResult(
                maskEmail(invitation.email()),
                tenant.name(),
                invitation.role(),
                invitation.expiresAt(),
                user == null);
    }

    @Override
    public AcceptResult accept(AcceptCommand command) {
        try {
            return Objects.requireNonNull(transactions.execute(
                    status -> acceptInTransaction(command)));
        } catch (DataIntegrityViolationException exception) {
            if (hasConstraint(exception, "uq_tenant_memberships_active_owner")) {
                throw ownerAlreadyAssigned();
            }
            if (hasConstraint(exception, "uq_tenant_memberships_tenant_user")) {
                throw userAlreadyMember();
            }
            throw exception;
        }
    }

    private AcceptResult acceptInTransaction(AcceptCommand command) {
        Instant now = clock.instant();
        TenantInvitation invitation = invitations
                .findByTokenHashForUpdate(tokens.hash(command.token()))
                .filter(value -> isUsableOwnerProvisioning(value, now))
                .orElseThrow(TenantInvitationService::invalidOrExpired);
        if (memberships.hasActiveOwner(invitation.tenantId())) {
            throw ownerAlreadyAssigned();
        }
        Tenant tenant = requireActiveTenant(invitation.tenantId());

        User user = users.findByEmail(invitation.email()).orElse(null);
        boolean accountCreated = false;
        if (user == null) {
            if (command.password() == null
                    || command.password().length() < 8
                    || command.fullName() == null
                    || command.fullName().isBlank()) {
                throw error(
                        HttpStatus.UNPROCESSABLE_ENTITY,
                        TenantInvitationErrorCodes.REGISTRATION_DETAILS_REQUIRED,
                        "Full name and a password of at least 8 characters are required for a new user.");
            }
            user = User.create(
                    invitation.email(),
                    passwordHasher.hash(command.password()),
                    command.fullName().trim(),
                    normalizeOptional(command.phone()),
                    now);
            users.save(user);
            accountCreated = true;
        } else if (!user.isActive()) {
            throw userInactive();
        }

        if (memberships.find(user.id(), tenant.id()).isPresent()) {
            throw userAlreadyMember();
        }

        memberships.add(TenantMembership.owner(tenant.id(), user.id(), now));
        invitations.save(invitation.accept(user.id(), now));
        UUID notificationId = UUID.randomUUID();
        ObjectNode payload = welcomePayload(notificationId, user, tenant);
        enqueue(
                WELCOME_EMAIL_EVENT,
                tenant.id(),
                user.id(),
                notificationId.toString(),
                payload,
                now);
        return new AcceptResult(user.id(), tenant.id(), invitation.role(), accountCreated);
    }

    private ObjectNode welcomePayload(UUID notificationId, User user, Tenant tenant) {
        ObjectNode payload = objectMapper.createObjectNode()
                .put("notificationId", notificationId.toString())
                .put("templateKey", "tenant-welcome");
        var recipients = payload.putArray("recipients");
        recipients.addObject()
                .put("address", user.email())
                .put("displayName", user.fullName());
        payload.putObject("variables")
                .put("userName", user.fullName())
                .put("tenantName", tenant.name())
                .put("roleName", "Tenant Owner");
        return payload;
    }

    private void enqueue(
            String eventType,
            UUID tenantId,
            UUID actorId,
            String partitionKey,
            ObjectNode payload,
            Instant now) {
        var context = ExecutionContext.require();
        IntegrationEventEnvelope envelope = new IntegrationEventEnvelope(
                UUID.randomUUID(),
                context.correlationId(),
                tenantId,
                actorId,
                OffsetDateTime.ofInstant(now, ZoneOffset.UTC),
                1,
                eventType,
                payload);
        outbox.enqueue(envelope, eventType, partitionKey);
    }

    private Tenant requireActiveTenant(UUID tenantId) {
        Tenant tenant = tenants.findByIdIncludingInactive(tenantId)
                .orElseThrow(() -> error(
                        HttpStatus.NOT_FOUND,
                        TenantErrorCodes.NOT_FOUND,
                        "Tenant was not found."));
        if (!tenant.isActive()) {
            throw error(
                    HttpStatus.FORBIDDEN,
                    TenantErrorCodes.INACTIVE,
                    "The tenant is inactive.");
        }
        return tenant;
    }

    private static boolean isUsableOwnerProvisioning(
            TenantInvitation invitation,
            Instant now) {
        return invitation.canAccept(now)
                && TenantInvitation.OWNER_ROLE.equals(invitation.role())
                && TenantInvitation.OWNER_PROVISIONING_PURPOSE.equals(invitation.purpose());
    }

    private static String maskEmail(String email) {
        int separator = email.indexOf('@');
        return separator <= 0 ? "***" : email.charAt(0) + "***" + email.substring(separator);
    }

    private static String normalizeOptional(String value) {
        return value == null || value.isBlank() ? null : value.trim();
    }

    private static StableApiException invalidOrExpired() {
        return error(
                HttpStatus.UNPROCESSABLE_ENTITY,
                TenantInvitationErrorCodes.INVALID_OR_EXPIRED,
                "The invitation is invalid, expired, or has already been used.");
    }

    private static StableApiException ownerAlreadyAssigned() {
        return error(
                HttpStatus.CONFLICT,
                TenantInvitationErrorCodes.OWNER_ALREADY_ASSIGNED,
                "The tenant already has an active owner.");
    }

    private static StableApiException ownerProvisioningPending() {
        return error(
                HttpStatus.CONFLICT,
                TenantInvitationErrorCodes.OWNER_PROVISIONING_ALREADY_PENDING,
                "The tenant already has an active owner provisioning request.");
    }

    private static StableApiException userAlreadyMember() {
        return error(
                HttpStatus.CONFLICT,
                TenantInvitationErrorCodes.USER_ALREADY_MEMBER,
                "The invited user is already a member of the current tenant.");
    }

    private static StableApiException userInactive() {
        return error(
                HttpStatus.CONFLICT,
                TenantInvitationErrorCodes.USER_INACTIVE,
                "The invited user account is not active.");
    }

    private static StableApiException tenantAccessDenied() {
        return error(
                HttpStatus.FORBIDDEN,
                TenantErrorCodes.ACCESS_DENIED,
                "System Administrator access is required.");
    }

    private static StableApiException error(HttpStatus status, String code, String message) {
        return new StableApiException(status, code, message);
    }

    private static boolean hasConstraint(Throwable exception, String constraint) {
        Throwable current = exception;
        String expected = constraint.toLowerCase(Locale.ROOT);
        while (current != null) {
            if (current.getMessage() != null
                    && current.getMessage().toLowerCase(Locale.ROOT).contains(expected)) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }
}

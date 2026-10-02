package com.agridrone.be1.identity.application.service.tenantinvitation;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatExceptionOfType;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

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
import com.agridrone.be1.identity.application.security.GeneratedInvitationToken;
import com.agridrone.be1.identity.domain.InvitationStatus;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantInvitation;
import com.agridrone.be1.identity.domain.TenantStatus;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.domain.UserStatus;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ActorType;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;
import com.agridrone.be1.shared.execution.ExecutionSource;
import com.agridrone.be1.shared.messaging.IntegrationEventEnvelope;
import com.agridrone.be1.shared.messaging.OutboxService;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.transaction.TransactionStatus;
import org.springframework.transaction.support.TransactionCallback;
import org.springframework.transaction.support.TransactionTemplate;

class TenantInvitationServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");
    private static final UUID ADMIN_ID = UUID.randomUUID();
    private static final UUID TENANT_ID = UUID.randomUUID();
    private static final String HASH = "A".repeat(64);

    private final UserRepository users = org.mockito.Mockito.mock(UserRepository.class);
    private final TenantRepository tenants = org.mockito.Mockito.mock(TenantRepository.class);
    private final TenantMembershipRepository memberships =
            org.mockito.Mockito.mock(TenantMembershipRepository.class);
    private final TenantInvitationRepository invitations =
            org.mockito.Mockito.mock(TenantInvitationRepository.class);
    private final InvitationTokenService tokens =
            org.mockito.Mockito.mock(InvitationTokenService.class);
    private final PasswordHasher passwordHasher = org.mockito.Mockito.mock(PasswordHasher.class);
    private final OutboxService outbox = org.mockito.Mockito.mock(OutboxService.class);
    private final TransactionTemplate transactions =
            org.mockito.Mockito.mock(TransactionTemplate.class);
    private TenantInvitationService service;

    @BeforeEach
    @SuppressWarnings("unchecked")
    void setUp() {
        when(transactions.execute(any())).thenAnswer(invocation -> {
            TransactionCallback<Object> callback = invocation.getArgument(0);
            return callback.doInTransaction(org.mockito.Mockito.mock(TransactionStatus.class));
        });
        TenantInvitationPolicy policy = () -> Duration.ofHours(72);
        service = new TenantInvitationService(
                users,
                tenants,
                memberships,
                invitations,
                tokens,
                passwordHasher,
                policy,
                outbox,
                new ObjectMapper().findAndRegisterModules(),
                Clock.fixed(NOW, ZoneOffset.UTC),
                transactions);
    }

    @Test
    void provisionsOwnerWithNormalizedEmailHashedTokenAndTransactionalOutbox() {
        when(tenants.findByIdIncludingInactive(TENANT_ID)).thenReturn(Optional.of(activeTenant()));
        when(users.findByEmail("owner@example.com")).thenReturn(Optional.empty());
        when(invitations.findPendingOwnerProvisioning(TENANT_ID)).thenReturn(Optional.empty());
        when(tokens.generate()).thenReturn(new GeneratedInvitationToken("plain-token", HASH));

        TenantInvitationUseCase.ProvisionResult result;
        try (var ignored = ExecutionContext.begin(adminContext())) {
            result = service.provisionOwner(TENANT_ID, " Owner@Example.com ");
        }

        ArgumentCaptor<TenantInvitation> invitation = ArgumentCaptor.forClass(TenantInvitation.class);
        verify(invitations).add(invitation.capture());
        assertThat(invitation.getValue().email()).isEqualTo("owner@example.com");
        assertThat(invitation.getValue().tokenHash()).isEqualTo(HASH);
        assertThat(invitation.getValue().expiresAt()).isEqualTo(NOW.plus(Duration.ofHours(72)));
        assertThat(result.invitationId()).isEqualTo(invitation.getValue().id());

        ArgumentCaptor<IntegrationEventEnvelope> envelope =
                ArgumentCaptor.forClass(IntegrationEventEnvelope.class);
        verify(outbox).enqueue(
                envelope.capture(),
                eq(TenantInvitationService.INVITATION_EMAIL_EVENT),
                eq(invitation.getValue().id().toString()));
        assertThat(envelope.getValue().payload().path("plainTextToken").asText())
                .isEqualTo("plain-token");
        assertThat(envelope.getValue().actorId()).isEqualTo(ADMIN_ID);
    }

    @Test
    void rejectsLivePendingOwnerProvisioningAndExpiresObsoleteOne() {
        when(tenants.findByIdIncludingInactive(TENANT_ID)).thenReturn(Optional.of(activeTenant()));
        when(users.findByEmail(any())).thenReturn(Optional.empty());
        TenantInvitation live = invitation(InvitationStatus.PENDING, NOW.plusSeconds(60));
        when(invitations.findPendingOwnerProvisioning(TENANT_ID)).thenReturn(Optional.of(live));

        try (var ignored = ExecutionContext.begin(adminContext())) {
            assertError(
                    () -> service.provisionOwner(TENANT_ID, "owner@example.com"),
                    TenantInvitationErrorCodes.OWNER_PROVISIONING_ALREADY_PENDING);
        }

        TenantInvitation obsolete = invitation(InvitationStatus.PENDING, NOW.minusSeconds(1));
        when(invitations.findPendingOwnerProvisioning(TENANT_ID)).thenReturn(Optional.of(obsolete));
        when(tokens.generate()).thenReturn(new GeneratedInvitationToken("new-token", HASH));
        try (var ignored = ExecutionContext.begin(adminContext())) {
            service.provisionOwner(TENANT_ID, "owner@example.com");
        }
        verify(invitations).save(org.mockito.ArgumentMatchers.argThat(
                value -> value.status() == InvitationStatus.EXPIRED));
    }

    @Test
    void previewsOnlyUsableInvitationAndMasksEmail() {
        TenantInvitation invitation = invitation(InvitationStatus.PENDING, NOW.plusSeconds(60));
        when(tokens.hash("plain-token")).thenReturn(HASH);
        when(invitations.findByTokenHash(HASH)).thenReturn(Optional.of(invitation));
        when(tenants.findByIdIncludingInactive(TENANT_ID)).thenReturn(Optional.of(activeTenant()));
        when(users.findByEmail(invitation.email())).thenReturn(Optional.empty());

        var result = service.preview("plain-token");

        assertThat(result.maskedEmail()).isEqualTo("o***@example.com");
        assertThat(result.requiresAccountCreation()).isTrue();

        when(invitations.findByTokenHash(HASH)).thenReturn(Optional.empty());
        assertError(
                () -> service.preview("plain-token"),
                TenantInvitationErrorCodes.INVALID_OR_EXPIRED);
    }

    @Test
    void previewRejectsInactiveUserAndInactiveTenant() {
        TenantInvitation invitation = invitation(InvitationStatus.PENDING, NOW.plusSeconds(60));
        when(tokens.hash(any())).thenReturn(HASH);
        when(invitations.findByTokenHash(HASH)).thenReturn(Optional.of(invitation));
        Tenant inactiveTenant = new Tenant(
                TENANT_ID, "TENANT", "Tenant", TenantStatus.INACTIVE, NOW, NOW, null);
        when(tenants.findByIdIncludingInactive(TENANT_ID)).thenReturn(Optional.of(inactiveTenant));
        assertError(() -> service.preview("token"), TenantErrorCodes.INACTIVE);

        when(tenants.findByIdIncludingInactive(TENANT_ID)).thenReturn(Optional.of(activeTenant()));
        when(users.findByEmail(invitation.email())).thenReturn(Optional.of(user(UserStatus.INACTIVE)));
        assertError(
                () -> service.preview("token"),
                TenantInvitationErrorCodes.USER_INACTIVE);
    }

    @Test
    void acceptsInvitationAtomicallyAndCreatesAccountMembershipAndWelcomeEvent() {
        TenantInvitation invitation = invitation(InvitationStatus.PENDING, NOW.plusSeconds(60));
        when(tokens.hash("plain-token")).thenReturn(HASH);
        when(invitations.findByTokenHashForUpdate(HASH)).thenReturn(Optional.of(invitation));
        when(tenants.findByIdIncludingInactive(TENANT_ID)).thenReturn(Optional.of(activeTenant()));
        when(users.findByEmail(invitation.email())).thenReturn(Optional.empty());
        when(passwordHasher.hash("Password123!")).thenReturn("password-hash");
        when(memberships.find(any(), eq(TENANT_ID))).thenReturn(Optional.empty());

        TenantInvitationUseCase.AcceptResult result;
        try (var ignored = ExecutionContext.begin(anonymousContext())) {
            result = service.accept(new TenantInvitationUseCase.AcceptCommand(
                    "plain-token", "Password123!", " Farm Owner ", " 0123 "));
        }

        assertThat(result.accountCreated()).isTrue();
        assertThat(result.tenantId()).isEqualTo(TENANT_ID);
        ArgumentCaptor<User> createdUser = ArgumentCaptor.forClass(User.class);
        verify(users).save(createdUser.capture());
        assertThat(createdUser.getValue().email()).isEqualTo(invitation.email());
        assertThat(createdUser.getValue().fullName()).isEqualTo("Farm Owner");
        assertThat(createdUser.getValue().phone()).isEqualTo("0123");
        verify(memberships).add(org.mockito.ArgumentMatchers.argThat(
                membership -> membership.userId().equals(createdUser.getValue().id())
                        && membership.tenantId().equals(TENANT_ID)));
        verify(invitations).save(org.mockito.ArgumentMatchers.argThat(
                value -> value.status() == InvitationStatus.ACCEPTED
                        && value.acceptedBy().equals(createdUser.getValue().id())));
        verify(outbox).enqueue(
                org.mockito.ArgumentMatchers.argThat(envelope ->
                        envelope.payload().path("templateKey").asText().equals("tenant-welcome")),
                eq(TenantInvitationService.WELCOME_EMAIL_EVENT),
                any());
    }

    @Test
    void newAccountRequiresRegistrationDetailsAndExistingMembershipIsRejected() {
        TenantInvitation invitation = invitation(InvitationStatus.PENDING, NOW.plusSeconds(60));
        when(tokens.hash(any())).thenReturn(HASH);
        when(invitations.findByTokenHashForUpdate(HASH)).thenReturn(Optional.of(invitation));
        when(tenants.findByIdIncludingInactive(TENANT_ID)).thenReturn(Optional.of(activeTenant()));
        when(users.findByEmail(invitation.email())).thenReturn(Optional.empty());

        try (var ignored = ExecutionContext.begin(anonymousContext())) {
            assertError(
                    () -> service.accept(new TenantInvitationUseCase.AcceptCommand(
                            "token", null, null, null)),
                    TenantInvitationErrorCodes.REGISTRATION_DETAILS_REQUIRED);
        }
        verify(memberships, never()).add(any());
    }

    private static Tenant activeTenant() {
        return new Tenant(TENANT_ID, "TENANT", "Tenant", TenantStatus.ACTIVE, NOW, NOW, null);
    }

    private static User user(UserStatus status) {
        return new User(
                UUID.randomUUID(), "owner@example.com", "hash", "Owner", null,
                status, null, NOW, NOW, null);
    }

    private static TenantInvitation invitation(InvitationStatus status, Instant expiresAt) {
        return new TenantInvitation(
                UUID.randomUUID(),
                TENANT_ID,
                "owner@example.com",
                TenantInvitation.OWNER_ROLE,
                TenantInvitation.OWNER_PROVISIONING_PURPOSE,
                HASH,
                status,
                ADMIN_ID,
                null,
                expiresAt,
                NOW.minusSeconds(120),
                null);
    }

    private static ExecutionContextSnapshot adminContext() {
        return new ExecutionContextSnapshot(
                null,
                ADMIN_ID,
                ActorType.USER,
                UUID.randomUUID(),
                null,
                ExecutionSource.HTTP,
                Set.of("SYSTEM_ADMIN"));
    }

    private static ExecutionContextSnapshot anonymousContext() {
        return new ExecutionContextSnapshot(
                null,
                null,
                ActorType.SYSTEM,
                UUID.randomUUID(),
                null,
                ExecutionSource.HTTP,
                Set.of());
    }

    private static void assertError(Runnable action, String code) {
        assertThatExceptionOfType(StableApiException.class)
                .isThrownBy(action::run)
                .satisfies(exception -> assertThat(exception.code()).isEqualTo(code));
    }
}

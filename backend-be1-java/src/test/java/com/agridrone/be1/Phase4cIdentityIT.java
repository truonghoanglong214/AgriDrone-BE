package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.error.TenantInvitationErrorCodes;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserCommand;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserUseCase;
import com.agridrone.be1.identity.application.port.in.selecttenant.SelectTenantUseCase;
import com.agridrone.be1.identity.application.port.in.tenantadmin.TenantAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantinvitation.TenantInvitationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantquery.TenantQueryUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.InvitationTokenService;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.application.security.GeneratedInvitationToken;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.auth.EffectiveTenantAccessGuard;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ActorType;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;
import com.agridrone.be1.shared.execution.ExecutionSource;
import com.nimbusds.jose.jwk.RSAKey;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Instant;
import java.util.HexFormat;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.security.oauth2.jose.jws.SignatureAlgorithm;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.utility.DockerImageName;

@SpringBootTest(properties = {
    "spring.jpa.hibernate.ddl-auto=none",
    "spring.task.scheduling.enabled=false",
    "agridrone.runtime.enabled=true",
    "agridrone.messaging.enabled=false",
    "agridrone.cache.enabled=false",
    "agridrone.security.jwt.enabled=false",
    "agridrone.security.jwt.issuer-service.enabled=true",
    "agridrone.security.jwt.issuer-service.issuer=https://phase4c.test",
    "agridrone.security.jwt.issuer-service.audience=agridrone-phase4c",
    "agridrone.security.jwt.issuer-service.private-key-location=classpath:contracts/auth/java-rs256-contract-private.pem",
    "agridrone.security.jwt.issuer-service.public-key-location=classpath:contracts/auth/java-rs256-contract-public.pem",
    "agridrone.security.jwt.issuer-service.key-id=phase4c-test-key"
})
@Testcontainers(disabledWithoutDocker = true)
class Phase4cIdentityIT {
    @Container
    static final PostgreSQLContainer<?> DATABASE = new PostgreSQLContainer<>(
            DockerImageName.parse("postgis/postgis:17-3.5")
                    .asCompatibleSubstituteFor("postgres"));

    @DynamicPropertySource
    static void databaseProperties(DynamicPropertyRegistry registry) {
        registry.add("spring.datasource.url", DATABASE::getJdbcUrl);
        registry.add("spring.datasource.username", DATABASE::getUsername);
        registry.add("spring.datasource.password", DATABASE::getPassword);
        registry.add("spring.flyway.enabled", () -> true);
    }

    @Autowired
    JdbcTemplate jdbc;

    @Autowired
    UserRepository users;

    @Autowired
    TenantMembershipRepository memberships;

    @Autowired
    PasswordHasher passwords;

    @Autowired
    TenantAdministrationUseCase tenantAdministration;

    @Autowired
    TenantInvitationUseCase tenantInvitations;

    @Autowired
    TenantQueryUseCase tenantQueries;

    @Autowired
    LoginUserUseCase login;

    @Autowired
    SelectTenantUseCase tenantSelection;

    @Autowired
    EffectiveTenantAccessGuard effectiveAccess;

    @Autowired
    RSAKey rsaKey;

    @Autowired
    DeterministicInvitationTokens invitationTokens;

    @BeforeEach
    void cleanDatabase() {
        jdbc.execute("""
                TRUNCATE
                    identity.tenant_invitations,
                    identity.tenant_memberships,
                    identity.user_roles,
                    identity.users,
                    identity.tenants,
                    messaging.outbox_messages,
                    audit.audit_logs
                CASCADE
                """);
        invitationTokens.reset();
    }

    @Test
    void verticalOwnerOnboardingSelectionAndTenantLifecycle() throws Exception {
        User admin = createUser("admin@phase4c.test", "AdminPass123!", "System Admin");
        UUID firstTenantId;
        String invitationToken;
        try (var ignored = ExecutionContext.begin(adminContext(admin.id()))) {
            firstTenantId = tenantAdministration.create(
                    new TenantAdministrationUseCase.CreateTenantCommand(
                            " phase-4c-a ", "Phase 4C Alpha"))
                    .tenantId();
            tenantInvitations.provisionOwner(firstTenantId, " OWNER@EXAMPLE.COM ");
            invitationToken = invitationTokens.latest();
        }

        var preview = tenantInvitations.preview(invitationToken);
        assertThat(preview.maskedEmail()).isEqualTo("o***@example.com");
        assertThat(preview.requiresAccountCreation()).isTrue();

        TenantInvitationUseCase.AcceptResult accepted;
        try (var ignored = ExecutionContext.begin(anonymousContext())) {
            accepted = tenantInvitations.accept(new TenantInvitationUseCase.AcceptCommand(
                    invitationToken, "OwnerPass123!", "Phase Owner", " 0909000000 "));
        }
        assertThat(accepted.accountCreated()).isTrue();
        assertThat(count("identity.tenant_memberships", "tenant_id", firstTenantId)).isOne();
        assertThat(eventCount(firstTenantId, "identity.tenant-invitation-email-requested.v1"))
                .isOne();
        assertThat(eventCount(firstTenantId, "notification.email-requested.v1")).isOne();

        var singleTenantLogin = login.login(
                new LoginUserCommand("owner@example.com", "OwnerPass123!"));
        assertThat(singleTenantLogin.session()).isNotNull();
        assertThat(singleTenantLogin.tenantSelection()).isNull();

        UUID secondTenantId;
        try (var ignored = ExecutionContext.begin(adminContext(admin.id()))) {
            secondTenantId = tenantAdministration.create(
                    new TenantAdministrationUseCase.CreateTenantCommand(
                            "PHASE-4C-B", "Phase 4C Beta"))
                    .tenantId();
        }
        memberships.add(TenantMembership.owner(secondTenantId, accepted.userId(), Instant.now()));

        var multiTenantLogin = login.login(
                new LoginUserCommand("owner@example.com", "OwnerPass123!"));
        assertThat(multiTenantLogin.session()).isNull();
        assertThat(multiTenantLogin.tenantSelection().tenants())
                .extracting(option -> option.id())
                .containsExactly(firstTenantId, secondTenantId);

        var selected = tenantSelection.select(new SelectTenantUseCase.SelectTenantCommand(
                multiTenantLogin.tenantSelection().selectionToken(), firstTenantId));
        var decoded = NimbusJwtDecoder.withPublicKey(rsaKey.toRSAPublicKey())
                .signatureAlgorithm(SignatureAlgorithm.RS256)
                .build()
                .decode(selected.session().accessToken());
        UUID membershipId = UUID.fromString(decoded.getClaimAsString("tenant_membership_id"));
        assertThat(decoded.getSubject()).isEqualTo(accepted.userId().toString());
        assertThat(decoded.getClaimAsString("tenant_id")).isEqualTo(firstTenantId.toString());
        assertThat(decoded.getClaimAsString("tenant_role")).isEqualTo("OWNER");
        assertThat(effectiveAccess.evaluate(accepted.userId(), firstTenantId, membershipId).granted())
                .isTrue();

        try (var ignored = ExecutionContext.begin(adminContext(admin.id()))) {
            tenantAdministration.deactivate(firstTenantId);
            tenantAdministration.deactivate(firstTenantId);
        }
        assertThat(tenantQueries.findTenants(PageRequest.defaults()).items())
                .anySatisfy(item -> {
                    assertThat(item.id()).isEqualTo(firstTenantId);
                    assertThat(item.status().name()).isEqualTo("INACTIVE");
                });
        assertThatThrownBy(() -> tenantSelection.select(
                new SelectTenantUseCase.SelectTenantCommand(
                        multiTenantLogin.tenantSelection().selectionToken(), firstTenantId)))
                .isInstanceOfSatisfying(StableApiException.class,
                        error -> assertThat(error.code()).isEqualTo(TenantErrorCodes.ACCESS_DENIED));
        assertThat(effectiveAccess.evaluate(accepted.userId(), firstTenantId, membershipId))
                .satisfies(decision -> {
                    assertThat(decision.granted()).isFalse();
                    assertThat(decision.errorCode()).isEqualTo(TenantErrorCodes.INACTIVE);
                });

        try (var ignored = ExecutionContext.begin(adminContext(admin.id()))) {
            tenantAdministration.activate(firstTenantId);
            tenantAdministration.activate(firstTenantId);
        }
        var freshSelection = login.login(
                new LoginUserCommand("owner@example.com", "OwnerPass123!"));
        var restored = tenantSelection.select(new SelectTenantUseCase.SelectTenantCommand(
                freshSelection.tenantSelection().selectionToken(), firstTenantId));
        assertThat(restored.session().tenant().id()).isEqualTo(firstTenantId);
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM audit.audit_logs WHERE entity_id = ?",
                Long.class,
                firstTenantId)).isEqualTo(3L);
        assertThat(tenantQueries.findUserTenants(accepted.userId(), PageRequest.defaults()).items())
                .hasSize(2);
    }

    @Test
    void concurrentProvisioningCreatesOnePendingInvitationAndOneOutboxMessage() throws Exception {
        User admin = createUser("race-admin@phase4c.test", "AdminPass123!", "Race Admin");
        UUID tenantId;
        try (var ignored = ExecutionContext.begin(adminContext(admin.id()))) {
            tenantId = tenantAdministration.create(
                    new TenantAdministrationUseCase.CreateTenantCommand(
                            "RACE-PROVISION", "Race Provision"))
                    .tenantId();
        }

        List<RaceOutcome> outcomes = runTogether(
                () -> provisionOutcome(admin.id(), tenantId, "one@example.com"),
                () -> provisionOutcome(admin.id(), tenantId, "two@example.com"));

        assertThat(outcomes).filteredOn(RaceOutcome::succeeded).hasSize(1);
        assertThat(outcomes).filteredOn(outcome -> !outcome.succeeded())
                .extracting(RaceOutcome::errorCode)
                .containsExactly(TenantInvitationErrorCodes.OWNER_PROVISIONING_ALREADY_PENDING);
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM identity.tenant_invitations WHERE tenant_id = ? AND status = 'PENDING'",
                Long.class,
                tenantId)).isOne();
        assertThat(eventCount(tenantId, "identity.tenant-invitation-email-requested.v1")).isOne();
    }

    @Test
    void concurrentAcceptanceCommitsExactlyOneOwnerAndOneWelcomeMessage() throws Exception {
        User admin = createUser("accept-admin@phase4c.test", "AdminPass123!", "Accept Admin");
        UUID tenantId;
        String token;
        try (var ignored = ExecutionContext.begin(adminContext(admin.id()))) {
            tenantId = tenantAdministration.create(
                    new TenantAdministrationUseCase.CreateTenantCommand(
                            "RACE-ACCEPT", "Race Accept"))
                    .tenantId();
            tenantInvitations.provisionOwner(tenantId, "accept-race@example.com");
            token = invitationTokens.latest();
        }

        List<RaceOutcome> outcomes = runTogether(
                () -> acceptOutcome(token),
                () -> acceptOutcome(token));

        assertThat(outcomes).filteredOn(RaceOutcome::succeeded).hasSize(1);
        assertThat(outcomes).filteredOn(outcome -> !outcome.succeeded())
                .extracting(RaceOutcome::errorCode)
                .containsExactly(TenantInvitationErrorCodes.INVALID_OR_EXPIRED);
        assertThat(count("identity.tenant_memberships", "tenant_id", tenantId)).isOne();
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM identity.tenant_invitations WHERE tenant_id = ? AND status = 'ACCEPTED'",
                Long.class,
                tenantId)).isOne();
        assertThat(eventCount(tenantId, "notification.email-requested.v1")).isOne();
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM identity.users WHERE email = 'accept-race@example.com'",
                Long.class)).isOne();
    }

    private User createUser(String email, String password, String fullName) {
        User user = User.create(email, passwords.hash(password), fullName, null, Instant.now());
        users.save(user);
        return user;
    }

    private RaceOutcome provisionOutcome(UUID adminId, UUID tenantId, String email) {
        try (var ignored = ExecutionContext.begin(adminContext(adminId))) {
            tenantInvitations.provisionOwner(tenantId, email);
            return RaceOutcome.success();
        } catch (StableApiException exception) {
            return RaceOutcome.failure(exception.code());
        }
    }

    private RaceOutcome acceptOutcome(String token) {
        try (var ignored = ExecutionContext.begin(anonymousContext())) {
            tenantInvitations.accept(new TenantInvitationUseCase.AcceptCommand(
                    token, "OwnerPass123!", "Concurrent Owner", null));
            return RaceOutcome.success();
        } catch (StableApiException exception) {
            return RaceOutcome.failure(exception.code());
        }
    }

    private static List<RaceOutcome> runTogether(
            Callable<RaceOutcome> first,
            Callable<RaceOutcome> second) throws Exception {
        CountDownLatch ready = new CountDownLatch(2);
        CountDownLatch start = new CountDownLatch(1);
        try (var executor = Executors.newFixedThreadPool(2)) {
            Future<RaceOutcome> firstFuture = executor.submit(awaitStart(ready, start, first));
            Future<RaceOutcome> secondFuture = executor.submit(awaitStart(ready, start, second));
            assertThat(ready.await(10, TimeUnit.SECONDS)).isTrue();
            start.countDown();
            return List.of(firstFuture.get(20, TimeUnit.SECONDS), secondFuture.get(20, TimeUnit.SECONDS));
        }
    }

    private static Callable<RaceOutcome> awaitStart(
            CountDownLatch ready,
            CountDownLatch start,
            Callable<RaceOutcome> action) {
        return () -> {
            ready.countDown();
            assertThat(start.await(10, TimeUnit.SECONDS)).isTrue();
            return action.call();
        };
    }

    private long eventCount(UUID tenantId, String eventType) {
        return jdbc.queryForObject(
                "SELECT count(*) FROM messaging.outbox_messages WHERE tenant_id = ? AND event_type = ?",
                Long.class,
                tenantId,
                eventType);
    }

    private long count(String table, String column, UUID value) {
        return jdbc.queryForObject(
                "SELECT count(*) FROM " + table + " WHERE " + column + " = ?",
                Long.class,
                value);
    }

    private static ExecutionContextSnapshot adminContext(UUID adminId) {
        return new ExecutionContextSnapshot(
                null,
                adminId,
                ActorType.USER,
                UUID.randomUUID(),
                null,
                ExecutionSource.HTTP,
                Set.of(SystemRoleCodes.SYSTEM_ADMIN));
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

    private record RaceOutcome(boolean succeeded, String errorCode) {
        static RaceOutcome success() {
            return new RaceOutcome(true, null);
        }

        static RaceOutcome failure(String errorCode) {
            return new RaceOutcome(false, errorCode);
        }
    }

    @TestConfiguration(proxyBeanMethods = false)
    static class InvitationTokenTestConfiguration {
        @Bean
        @Primary
        DeterministicInvitationTokens deterministicInvitationTokens() {
            return new DeterministicInvitationTokens();
        }
    }

    static final class DeterministicInvitationTokens implements InvitationTokenService {
        private final AtomicInteger sequence = new AtomicInteger();
        private volatile String latest;

        @Override
        public GeneratedInvitationToken generate() {
            String token = "phase4c-invitation-" + sequence.incrementAndGet();
            latest = token;
            return new GeneratedInvitationToken(token, hash(token));
        }

        @Override
        public String hash(String plainTextToken) {
            try {
                return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256")
                        .digest(plainTextToken.getBytes(StandardCharsets.UTF_8)));
            } catch (java.security.NoSuchAlgorithmException exception) {
                throw new IllegalStateException(exception);
            }
        }

        String latest() {
            return latest;
        }

        void reset() {
            sequence.set(0);
            latest = null;
        }
    }
}

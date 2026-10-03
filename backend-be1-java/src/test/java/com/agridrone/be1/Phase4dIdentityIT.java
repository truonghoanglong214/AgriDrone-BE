package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.agridrone.be1.identity.application.error.SystemManagerErrorCodes;
import com.agridrone.be1.identity.application.error.SystemManagerInvitationErrorCodes;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAccessDecisionUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerInvitationUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerWorkUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.InvitationTokenService;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.application.security.GeneratedInvitationToken;
import com.agridrone.be1.identity.domain.ManagerAvailability;
import com.agridrone.be1.identity.domain.QualificationStatus;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ActorType;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;
import com.agridrone.be1.shared.execution.ExecutionSource;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.sql.Timestamp;
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
import java.util.function.Supplier;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;
import org.springframework.jdbc.core.JdbcTemplate;
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
    "agridrone.security.jwt.issuer-service.enabled=false",
    "agridrone.notification.smtp.enabled=false"
})
@Testcontainers(disabledWithoutDocker = true)
class Phase4dIdentityIT {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");

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
    PasswordHasher passwords;

    @Autowired
    SystemManagerAdministrationUseCase administration;

    @Autowired
    SystemManagerInvitationUseCase invitations;

    @Autowired
    SystemManagerWorkUseCase work;

    @Autowired
    SystemManagerAccessDecisionUseCase access;

    @Autowired
    DeterministicManagerInvitationTokens invitationTokens;

    @BeforeEach
    void cleanDatabase() {
        jdbc.execute("""
                TRUNCATE
                    identity.system_manager_invitations,
                    identity.farm_manager_assignments,
                    identity.system_manager_profiles,
                    farm.farms,
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
    void invitationLifecycleAssignmentAndAccessFlowAreComplete() {
        UUID adminId = createUser("admin@example.com", "Admin");
        UUID tenantId = insertTenant("TENANT-A");
        UUID farmId = insertFarm(tenantId, "FARM-A");
        UUID otherTenantId = insertTenant("TENANT-B");
        UUID otherFarmId = insertFarm(otherTenantId, "FARM-B");

        var invite = withContext(adminContext(adminId),
                () -> invitations.invite("manager@example.com"));
        assertThat(invite.emailSent()).isFalse();
        assertThat(invitations.preview(invitationTokens.latest()))
                .extracting(
                        SystemManagerInvitationUseCase.PreviewResult::maskedEmail,
                        SystemManagerInvitationUseCase.PreviewResult::role,
                        SystemManagerInvitationUseCase.PreviewResult::requiresAccountCreation)
                .containsExactly("m***@example.com", "SYSTEM_MANAGER", true);

        var accepted = withContext(anonymousContext(), () -> invitations.accept(
                new SystemManagerInvitationUseCase.AcceptCommand(
                        invitationTokens.latest(), "Password123!", "Manager", null)));
        assertThat(accepted.accountCreated()).isTrue();

        var active = withContext(adminContext(adminId), () -> administration.activate(
                accepted.profileId(), "employment approved", 1));
        var qualified = withContext(adminContext(adminId), () ->
                administration.updateQualification(
                        accepted.profileId(),
                        QualificationStatus.QUALIFIED,
                        NOW.plusSeconds(86400),
                        "flight certificate verified",
                        active.version()));
        var available = withContext(adminContext(adminId), () ->
                administration.updateAvailability(
                        accepted.profileId(),
                        ManagerAvailability.AVAILABLE,
                        "ready for assignment",
                        qualified.version()));
        var assignment = withContext(adminContext(adminId), () ->
                administration.assignPrimary(
                        farmId,
                        accepted.profileId(),
                        "primary operator",
                        null));
        assertThat(assignment.version()).isOne();

        var managerContext = managerContext(accepted.userId());
        assertThat(withContext(managerContext, work::findAssignedFarms))
                .singleElement()
                .extracting(SystemManagerWorkUseCase.AssignedFarmResult::farmId)
                .isEqualTo(farmId);
        assertThat(withContext(managerContext, () -> access.requireFarmAccess(farmId)))
                .extracting(
                        SystemManagerAccessDecisionUseCase.FarmAccess::tenantId,
                        SystemManagerAccessDecisionUseCase.FarmAccess::profileId)
                .containsExactly(tenantId, accepted.profileId());

        assertStableError(
                SystemManagerErrorCodes.ACCESS_DENIED,
                () -> withContext(managerContext, () -> access.requireFarmAccess(otherFarmId)));

        var suspended = withContext(adminContext(adminId), () -> administration.suspend(
                accepted.profileId(), "temporary suspension", available.version()));
        assertStableError(
                SystemManagerErrorCodes.NOT_ASSIGNABLE,
                () -> withContext(managerContext, () -> access.requireFarmAccess(farmId)));

        var reactivated = withContext(adminContext(adminId), () -> administration.activate(
                accepted.profileId(), "suspension cleared", suspended.version()));
        jdbc.update("UPDATE identity.users SET status='INACTIVE' WHERE id=?", accepted.userId());
        assertStableError(
                SystemManagerErrorCodes.NOT_ASSIGNABLE,
                () -> withContext(managerContext, work::findAssignedFarms));

        jdbc.update("UPDATE identity.users SET status='ACTIVE' WHERE id=?", accepted.userId());
        withContext(adminContext(adminId), () -> {
            administration.endPrimary(farmId, "assignment ended", assignment.version());
            return Boolean.TRUE;
        });
        assertStableError(
                SystemManagerErrorCodes.ACCESS_DENIED,
                () -> withContext(managerContext, () -> access.requireFarmAccess(farmId)));

        assertThat(reactivated.version()).isEqualTo(6);
        assertThat(count("identity.farm_manager_assignments", "farm_id", farmId)).isOne();
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM identity.farm_manager_assignments "
                        + "WHERE farm_id=? AND ended_at IS NULL",
                Long.class,
                farmId)).isZero();
    }

    @Test
    void concurrentPrimaryAssignmentCreatesExactlyOneActiveManager() throws Exception {
        UUID adminId = createUser("admin@example.com", "Admin");
        UUID tenantId = insertTenant("TENANT-A");
        UUID farmId = insertFarm(tenantId, "FARM-A");
        var first = createAssignableManager(adminId, "first@example.com");
        var second = createAssignableManager(adminId, "second@example.com");

        List<RaceOutcome> outcomes = race(
                () -> assignOutcome(adminId, farmId, first.id()),
                () -> assignOutcome(adminId, farmId, second.id()));

        assertThat(outcomes).filteredOn(RaceOutcome::succeeded).hasSize(1);
        assertThat(outcomes).filteredOn(outcome -> !outcome.succeeded())
                .singleElement()
                .extracting(RaceOutcome::errorCode)
                .isEqualTo(SystemManagerErrorCodes.ACTIVE_ASSIGNMENT_CONFLICT);
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM identity.farm_manager_assignments "
                        + "WHERE farm_id=? AND ended_at IS NULL",
                Long.class,
                farmId)).isOne();
    }

    @Test
    void concurrentInvitationAcceptanceCreatesOneProfile() throws Exception {
        UUID adminId = createUser("admin@example.com", "Admin");
        withContext(adminContext(adminId), () -> invitations.invite("manager@example.com"));
        String token = invitationTokens.latest();

        List<RaceOutcome> outcomes = race(
                () -> acceptOutcome(token),
                () -> acceptOutcome(token));

        assertThat(outcomes).filteredOn(RaceOutcome::succeeded).hasSize(1);
        assertThat(outcomes).filteredOn(outcome -> !outcome.succeeded())
                .singleElement()
                .extracting(RaceOutcome::errorCode)
                .isEqualTo(SystemManagerInvitationErrorCodes.INVALID_OR_EXPIRED);
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM identity.system_manager_profiles",
                Long.class)).isOne();
        assertThat(jdbc.queryForObject(
                "SELECT count(*) FROM identity.system_manager_invitations "
                        + "WHERE status='ACCEPTED'",
                Long.class)).isOne();
    }

    private SystemManagerAdministrationUseCase.ProfileResult createAssignableManager(
            UUID adminId,
            String email) {
        UUID userId = createUser(email, email.substring(0, email.indexOf('@')));
        return withContext(adminContext(adminId), () -> {
            var created = administration.createProfile(userId);
            var active = administration.activate(created.id(), "active", created.version());
            var qualified = administration.updateQualification(
                    created.id(), QualificationStatus.QUALIFIED, NOW.plusSeconds(86400),
                    "qualified", active.version());
            return administration.updateAvailability(
                    created.id(), ManagerAvailability.AVAILABLE, "available", qualified.version());
        });
    }

    private RaceOutcome assignOutcome(UUID adminId, UUID farmId, UUID profileId) {
        try {
            withContext(adminContext(adminId), () -> administration.assignPrimary(
                    farmId, profileId, "concurrent assignment", null));
            return RaceOutcome.success();
        } catch (StableApiException exception) {
            return RaceOutcome.failure(exception.code());
        }
    }

    private RaceOutcome acceptOutcome(String token) {
        try {
            withContext(anonymousContext(), () -> invitations.accept(
                    new SystemManagerInvitationUseCase.AcceptCommand(
                            token, "Password123!", "Manager", null)));
            return RaceOutcome.success();
        } catch (StableApiException exception) {
            return RaceOutcome.failure(exception.code());
        }
    }

    private UUID createUser(String email, String name) {
        User user = User.create(email, passwords.hash("Password123!"), name, null, NOW);
        users.save(user);
        return user.id();
    }

    private UUID insertTenant(String code) {
        UUID id = UUID.randomUUID();
        jdbc.update(
                "INSERT INTO identity.tenants(id,code,name,status,created_at,updated_at) "
                        + "VALUES (?,?,?,'ACTIVE',?,?)",
                id, code, code + " Name", Timestamp.from(NOW), Timestamp.from(NOW));
        return id;
    }

    private UUID insertFarm(UUID tenantId, String code) {
        UUID id = UUID.randomUUID();
        jdbc.update(
                "INSERT INTO farm.farms(id,tenant_id,code,name,status,created_at,updated_at) "
                        + "VALUES (?,?,?,?,'ACTIVE',?,?)",
                id, tenantId, code, code + " Name", Timestamp.from(NOW), Timestamp.from(NOW));
        return id;
    }

    private long count(String table, String column, UUID value) {
        return jdbc.queryForObject(
                "SELECT count(*) FROM " + table + " WHERE " + column + " = ?",
                Long.class,
                value);
    }

    private static void assertStableError(String code, Runnable action) {
        assertThatThrownBy(action::run)
                .isInstanceOf(StableApiException.class)
                .extracting(exception -> ((StableApiException) exception).code())
                .isEqualTo(code);
    }

    private static <T> T withContext(
            ExecutionContextSnapshot context,
            Supplier<T> action) {
        try (ExecutionContext.Scope ignored = ExecutionContext.begin(context)) {
            return action.get();
        }
    }

    private static List<RaceOutcome> race(
            Callable<RaceOutcome> first,
            Callable<RaceOutcome> second) throws Exception {
        CountDownLatch ready = new CountDownLatch(2);
        CountDownLatch start = new CountDownLatch(1);
        try (var executor = Executors.newFixedThreadPool(2)) {
            Future<RaceOutcome> firstFuture = executor.submit(awaitStart(ready, start, first));
            Future<RaceOutcome> secondFuture = executor.submit(awaitStart(ready, start, second));
            assertThat(ready.await(10, TimeUnit.SECONDS)).isTrue();
            start.countDown();
            return List.of(
                    firstFuture.get(20, TimeUnit.SECONDS),
                    secondFuture.get(20, TimeUnit.SECONDS));
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

    private static ExecutionContextSnapshot adminContext(UUID adminId) {
        return new ExecutionContextSnapshot(
                null, adminId, ActorType.USER, UUID.randomUUID(), null,
                ExecutionSource.HTTP, Set.of(SystemRoleCodes.SYSTEM_ADMIN));
    }

    private static ExecutionContextSnapshot managerContext(UUID managerId) {
        return new ExecutionContextSnapshot(
                null, managerId, ActorType.USER, UUID.randomUUID(), null,
                ExecutionSource.HTTP, Set.of(SystemRoleCodes.SYSTEM_MANAGER));
    }

    private static ExecutionContextSnapshot anonymousContext() {
        return new ExecutionContextSnapshot(
                null, null, ActorType.SYSTEM, UUID.randomUUID(), null,
                ExecutionSource.HTTP, Set.of());
    }

    private record RaceOutcome(boolean succeeded, String errorCode) {
        static RaceOutcome success() { return new RaceOutcome(true, null); }
        static RaceOutcome failure(String errorCode) {
            return new RaceOutcome(false, errorCode);
        }
    }

    @TestConfiguration(proxyBeanMethods = false)
    static class InvitationTokenTestConfiguration {
        @Bean
        @Primary
        DeterministicManagerInvitationTokens deterministicManagerInvitationTokens() {
            return new DeterministicManagerInvitationTokens();
        }
    }

    static final class DeterministicManagerInvitationTokens
            implements InvitationTokenService {
        private final AtomicInteger sequence = new AtomicInteger();
        private volatile String latest;

        @Override
        public GeneratedInvitationToken generate() {
            String token = "phase4d-invitation-" + sequence.incrementAndGet();
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

        String latest() { return latest; }

        void reset() {
            sequence.set(0);
            latest = null;
        }
    }
}

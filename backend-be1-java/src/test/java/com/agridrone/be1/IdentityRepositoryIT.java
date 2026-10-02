package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.agridrone.be1.identity.application.port.out.persistence.FarmManagerAssignmentRepository;
import com.agridrone.be1.identity.application.port.out.persistence.PasswordResetTokenRepository;
import com.agridrone.be1.identity.application.port.out.persistence.RoleRepository;
import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerInvitationRepository;
import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerProfileRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantInvitationRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.domain.FarmManagerAssignment;
import com.agridrone.be1.identity.domain.InvitationStatus;
import com.agridrone.be1.identity.domain.ManagerAvailability;
import com.agridrone.be1.identity.domain.PasswordResetToken;
import com.agridrone.be1.identity.domain.QualificationStatus;
import com.agridrone.be1.identity.domain.SystemManagerInvitation;
import com.agridrone.be1.identity.domain.SystemManagerProfile;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantInvitation;
import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.domain.TenantStatus;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.domain.UserStatus;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.FarmManagerAssignmentPersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.PasswordResetTokenPersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.RolePersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.SystemManagerInvitationPersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.SystemManagerProfilePersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.TenantInvitationPersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.TenantMembershipPersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.TenantPersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.UserPersistenceAdapter;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.support.TransactionTemplate;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.utility.DockerImageName;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=none")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import({
        UserPersistenceAdapter.class,
        TenantPersistenceAdapter.class,
        TenantMembershipPersistenceAdapter.class,
        SystemManagerProfilePersistenceAdapter.class,
        FarmManagerAssignmentPersistenceAdapter.class,
        TenantInvitationPersistenceAdapter.class,
        SystemManagerInvitationPersistenceAdapter.class,
        PasswordResetTokenPersistenceAdapter.class,
        RolePersistenceAdapter.class
})
@Testcontainers(disabledWithoutDocker = true)
@Transactional(propagation = Propagation.NOT_SUPPORTED)
class IdentityRepositoryIT {
    private static final Instant NOW = Instant.parse("2026-09-28T00:00:00Z");

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
    TenantRepository tenants;

    @Autowired
    TenantMembershipRepository memberships;

    @Autowired
    SystemManagerProfileRepository profiles;

    @Autowired
    FarmManagerAssignmentRepository assignments;

    @Autowired
    TenantInvitationRepository tenantInvitations;

    @Autowired
    SystemManagerInvitationRepository systemManagerInvitations;

    @Autowired
    PasswordResetTokenRepository passwordResetTokens;

    @Autowired
    RoleRepository roles;

    @Autowired
    PlatformTransactionManager transactionManager;

    @BeforeEach
    void cleanDatabase() {
        jdbc.execute("""
                TRUNCATE
                    identity.farm_manager_assignments,
                    farm.farms,
                    identity.system_manager_profiles,
                    identity.tenant_memberships,
                    identity.user_roles,
                    identity.users,
                    identity.tenants
                CASCADE
                """);
    }

    @Test
    void repositoriesRoundTripIdentityAggregates() {
        User owner = User.create("OWNER@EXAMPLE.COM", "bcrypt", "Owner", null, NOW);
        Tenant tenant = Tenant.create("T-1", "Farm tenant", NOW);
        TenantMembership membership = TenantMembership.owner(tenant.id(), owner.id(), NOW);

        users.save(owner);
        tenants.save(tenant);
        memberships.add(membership);

        assertThat(users.findByEmail("owner@example.com"))
                .get()
                .extracting(User::id)
                .isEqualTo(owner.id());
        assertThat(tenants.findByCode("T-1")).isPresent();
        assertThat(memberships.findActive(owner.id(), tenant.id())).contains(membership);
        assertThat(memberships.hasActiveOwner(tenant.id())).isTrue();
    }

    @Test
    void databaseAllowsOnlyOneActiveOwnerAndOnePrimaryManagerPerFarm() {
        User admin = User.create("admin@example.com", "hash", "Admin", null, NOW);
        User firstManager = User.create("first@example.com", "hash", "First", null, NOW);
        User secondManager = User.create("second@example.com", "hash", "Second", null, NOW);
        Tenant tenant = Tenant.create("T-2", "Tenant", NOW);

        users.save(admin);
        users.save(firstManager);
        users.save(secondManager);
        tenants.save(tenant);
        memberships.add(TenantMembership.owner(tenant.id(), firstManager.id(), NOW));

        assertThatThrownBy(() -> memberships.add(
                        TenantMembership.owner(tenant.id(), secondManager.id(), NOW)))
                .isInstanceOf(DataIntegrityViolationException.class);

        UUID farmId = UUID.randomUUID();
        jdbc.update(
                "INSERT INTO farm.farms(id, tenant_id, code, name) VALUES (?, ?, ?, ?)",
                farmId,
                tenant.id(),
                "F-1",
                "Farm");

        SystemManagerProfile firstProfile = saveQualified(firstManager.id());
        SystemManagerProfile secondProfile = saveQualified(secondManager.id());

        assignments.add(FarmManagerAssignment.create(
                tenant.id(), farmId, firstProfile.id(), admin.id(), "first", NOW));

        assertThatThrownBy(() -> assignments.add(FarmManagerAssignment.create(
                        tenant.id(), farmId, secondProfile.id(), admin.id(), "second", NOW)))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    void tokenRepositoriesPersistSingleUseTransitions() {
        User admin = User.create("admin@example.com", "hash", "Admin", null, NOW);
        User target = User.create("target@example.com", "hash", "Target", null, NOW);
        Tenant tenant = Tenant.create("T-3", "Tenant", NOW);

        users.save(admin);
        users.save(target);
        tenants.save(tenant);

        verifyTenantInvitation(admin, target, tenant);
        verifySystemManagerInvitation(admin, target);
        verifyPasswordResetToken(target);
    }

    @Test
    void seededRolesAreAvailableAndOnlyActiveUsersCountAsRoleHolders() {
        var adminRole = roles.findByCode(SystemRoleCodes.SYSTEM_ADMIN).orElseThrow();
        var managerRole = roles.findByCode(SystemRoleCodes.SYSTEM_MANAGER).orElseThrow();

        assertThat(adminRole.id())
                .isEqualTo(UUID.fromString("00000000-0000-0000-0000-000000000001"));
        assertThat(adminRole.code()).isEqualTo(SystemRoleCodes.SYSTEM_ADMIN);
        assertThat(managerRole.id())
                .isEqualTo(UUID.fromString("00000000-0000-0000-0000-000000000002"));
        assertThat(managerRole.code()).isEqualTo(SystemRoleCodes.SYSTEM_MANAGER);
        assertThat(roles.findByCode("UNKNOWN_ROLE")).isEmpty();

        User activeAdmin = User.create("active-admin@example.com", "hash", "Admin", null, NOW);
        User inactiveManager = new User(
                UUID.randomUUID(),
                "inactive-manager@example.com",
                "hash",
                "Inactive manager",
                null,
                UserStatus.INACTIVE,
                null,
                NOW,
                NOW,
                null);

        users.save(activeAdmin);
        users.save(inactiveManager);
        users.assignSystemRole(activeAdmin.id(), adminRole.id());
        users.assignSystemRole(inactiveManager.id(), managerRole.id());

        assertThat(roles.existsActiveUserWithRole(SystemRoleCodes.SYSTEM_ADMIN)).isTrue();
        assertThat(roles.existsActiveUserWithRole(SystemRoleCodes.SYSTEM_MANAGER)).isFalse();
    }

    @Test
    void tenantPageIncludesInactiveRowsAndUsesDeterministicOneBasedPaging() {
        Tenant older = new Tenant(
                UUID.fromString("00000000-0000-0000-0000-000000000010"),
                "OLDER",
                "Older tenant",
                TenantStatus.ACTIVE,
                NOW.minus(2, ChronoUnit.DAYS),
                NOW.minus(2, ChronoUnit.DAYS),
                null);
        Tenant newerInactive = new Tenant(
                UUID.fromString("00000000-0000-0000-0000-000000000020"),
                "NEWER",
                "Newer tenant",
                TenantStatus.INACTIVE,
                NOW.minus(1, ChronoUnit.DAYS),
                NOW,
                null);
        Tenant deleted = new Tenant(
                UUID.fromString("00000000-0000-0000-0000-000000000030"),
                "DELETED",
                "Deleted tenant",
                TenantStatus.INACTIVE,
                NOW,
                NOW,
                NOW);

        tenants.save(older);
        tenants.save(newerInactive);
        tenants.save(deleted);

        var firstPage = tenants.findPage(new com.agridrone.be1.shared.api.PageRequest(1, 1));
        var secondPage = tenants.findPage(new com.agridrone.be1.shared.api.PageRequest(2, 1));

        assertThat(firstPage.items()).extracting(item -> item.id())
                .containsExactly(newerInactive.id());
        assertThat(firstPage.totalCount()).isEqualTo(2);
        assertThat(firstPage.totalPages()).isEqualTo(2);
        assertThat(firstPage.hasPreviousPage()).isFalse();
        assertThat(firstPage.hasNextPage()).isTrue();
        assertThat(secondPage.items()).extracting(item -> item.id())
                .containsExactly(older.id());
        assertThat(secondPage.hasPreviousPage()).isTrue();
        assertThat(secondPage.hasNextPage()).isFalse();
        assertThat(tenants.findByIdIncludingInactive(newerInactive.id()))
                .get()
                .satisfies(found -> {
                    assertThat(found.id()).isEqualTo(newerInactive.id());
                    assertThat(found.status()).isEqualTo(TenantStatus.INACTIVE);
                });
        assertThat(tenants.findByIdIncludingInactive(deleted.id())).isEmpty();
        assertThat(tenants.existsByNormalizedCode(" newer ")).isTrue();
    }

    @Test
    void userTenantPageUsesProjectionAndExcludesInactiveMemberships() {
        User owner = User.create("owner@example.com", "hash", "Owner", null, NOW);
        Tenant older = tenantAt("MEMBERSHIP-OLDER", NOW.minus(2, ChronoUnit.DAYS));
        Tenant newer = tenantAt("MEMBERSHIP-NEWER", NOW.minus(1, ChronoUnit.DAYS));
        Tenant inactiveMembershipTenant = tenantAt("MEMBERSHIP-INACTIVE", NOW);
        users.save(owner);
        tenants.save(older);
        tenants.save(newer);
        tenants.save(inactiveMembershipTenant);

        TenantMembership olderMembership = membershipAt(
                owner.id(), older.id(), "ACTIVE", NOW.minus(2, ChronoUnit.DAYS));
        TenantMembership newerMembership = membershipAt(
                owner.id(), newer.id(), "ACTIVE", NOW.minus(1, ChronoUnit.DAYS));
        TenantMembership inactiveMembership = membershipAt(
                owner.id(), inactiveMembershipTenant.id(), "INACTIVE", NOW);
        memberships.add(olderMembership);
        memberships.add(newerMembership);
        memberships.add(inactiveMembership);

        var page = memberships.findPageByUserId(
                owner.id(),
                new com.agridrone.be1.shared.api.PageRequest(1, 20));

        assertThat(page.items()).extracting(item -> item.id())
                .containsExactly(newerMembership.id(), olderMembership.id());
        assertThat(page.totalCount()).isEqualTo(2);
        assertThat(memberships.find(owner.id(), inactiveMembershipTenant.id()))
                .contains(inactiveMembership);
        assertThat(memberships.findActive(owner.id(), inactiveMembershipTenant.id()))
                .isEmpty();
    }

    @Test
    void databaseAllowsOnlyOnePendingOwnerProvisioningPerTenant() {
        User admin = User.create("admin@example.com", "hash", "Admin", null, NOW);
        Tenant tenant = Tenant.create("OWNER-PENDING", "Tenant", NOW);
        users.save(admin);
        tenants.save(tenant);

        tenantInvitations.add(invitation(
                tenant.id(), admin.id(), "first@example.com", "d".repeat(64)));

        assertThatThrownBy(() -> tenantInvitations.add(invitation(
                        tenant.id(), admin.id(), "second@example.com", "e".repeat(64))))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    void invitationLookupHoldsPessimisticLockUntilOuterTransactionCompletes()
            throws Exception {
        User admin = User.create("admin@example.com", "hash", "Admin", null, NOW);
        Tenant tenant = Tenant.create("LOCK", "Tenant", NOW);
        TenantInvitation invitation = invitation(
                tenant.id(), admin.id(), "owner@example.com", "f".repeat(64));
        users.save(admin);
        tenants.save(tenant);
        tenantInvitations.add(invitation);

        ExecutorService executor = Executors.newFixedThreadPool(2);
        CountDownLatch firstHasLock = new CountDownLatch(1);
        CountDownLatch releaseFirst = new CountDownLatch(1);
        CountDownLatch secondStarted = new CountDownLatch(1);
        TransactionTemplate transactions = new TransactionTemplate(transactionManager);

        try {
            Future<?> first = executor.submit(() -> transactions.executeWithoutResult(status -> {
                tenantInvitations.findByTokenHashForUpdate(invitation.tokenHash())
                        .orElseThrow();
                firstHasLock.countDown();
                await(releaseFirst);
            }));
            assertThat(firstHasLock.await(5, TimeUnit.SECONDS)).isTrue();

            Future<TenantInvitation> second = executor.submit(() -> {
                secondStarted.countDown();
                return transactions.execute(status -> tenantInvitations
                        .findByTokenHashForUpdate(invitation.tokenHash())
                        .orElseThrow());
            });
            assertThat(secondStarted.await(5, TimeUnit.SECONDS)).isTrue();
            assertThatThrownBy(() -> second.get(250, TimeUnit.MILLISECONDS))
                    .isInstanceOf(TimeoutException.class);

            releaseFirst.countDown();
            first.get(5, TimeUnit.SECONDS);
            assertThat(second.get(5, TimeUnit.SECONDS)).isEqualTo(invitation);
        } finally {
            releaseFirst.countDown();
            executor.shutdownNow();
            assertThat(executor.awaitTermination(5, TimeUnit.SECONDS)).isTrue();
        }
    }

    private void verifyTenantInvitation(User admin, User target, Tenant tenant) {
        TenantInvitation invitation = invitation(
                tenant.id(), admin.id(), target.email(), "a".repeat(64));

        tenantInvitations.add(invitation);

        assertThat(tenantInvitations.findByTokenHash(invitation.tokenHash()))
                .contains(invitation);
        assertThat(tenantInvitations.findByTokenHashForUpdate(invitation.tokenHash()))
                .contains(invitation);
        assertThat(tenantInvitations.findPendingOwnerProvisioning(tenant.id()))
                .contains(invitation);
        assertThat(tenantInvitations.markAccepted(
                        invitation.id(), target.id(), NOW.plus(1, ChronoUnit.HOURS)))
                .isTrue();
        assertThat(tenantInvitations.markAccepted(
                        invitation.id(), target.id(), NOW.plus(2, ChronoUnit.HOURS)))
                .isFalse();

        TenantInvitation expired = invitation(
                tenant.id(), admin.id(), "expired@example.com", "9".repeat(64));
        tenantInvitations.add(expired);
        TenantInvitation transitioned = expired.expire(NOW.plus(2, ChronoUnit.DAYS));
        tenantInvitations.save(transitioned);

        assertThat(tenantInvitations.findByTokenHash(expired.tokenHash()))
                .contains(transitioned);
        assertThat(tenantInvitations.findPendingOwnerProvisioning(tenant.id()))
                .isEmpty();
    }

    private void verifySystemManagerInvitation(User admin, User target) {
        SystemManagerInvitation invitation = new SystemManagerInvitation(
                UUID.randomUUID(),
                target.email(),
                "b".repeat(64),
                InvitationStatus.PENDING,
                admin.id(),
                null,
                NOW.plus(1, ChronoUnit.DAYS),
                NOW,
                null);

        systemManagerInvitations.add(invitation);

        assertThat(systemManagerInvitations.findByTokenHashForUpdate(invitation.tokenHash()))
                .contains(invitation);
        assertThat(systemManagerInvitations.markAccepted(
                        invitation.id(), target.id(), NOW.plus(1, ChronoUnit.HOURS)))
                .isTrue();
    }

    private void verifyPasswordResetToken(User target) {
        PasswordResetToken token = new PasswordResetToken(
                UUID.randomUUID(),
                target.id(),
                "c".repeat(64),
                NOW.plus(1, ChronoUnit.HOURS),
                NOW,
                null,
                null);

        passwordResetTokens.add(token);

        assertThat(passwordResetTokens.findByTokenHashForUpdate(token.tokenHash()))
                .contains(token);
        assertThat(passwordResetTokens.markUsed(
                        token.id(), NOW.plus(5, ChronoUnit.MINUTES)))
                .isTrue();
        assertThat(passwordResetTokens.markUsed(
                        token.id(), NOW.plus(10, ChronoUnit.MINUTES)))
                .isFalse();
    }

    private SystemManagerProfile saveQualified(UUID userId) {
        SystemManagerProfile profile = SystemManagerProfile.create(userId, NOW);
        profiles.save(profile);

        profile.activate(NOW, 1);
        profiles.save(profile);

        profile.updateQualification(
                QualificationStatus.QUALIFIED,
                NOW.plus(30, ChronoUnit.DAYS),
                NOW,
                2);
        profiles.save(profile);

        profile.updateAvailability(ManagerAvailability.AVAILABLE, NOW, 3);
        profiles.save(profile);

        return profile;
    }

    private static Tenant tenantAt(String code, Instant createdAt) {
        return new Tenant(
                UUID.randomUUID(),
                code,
                code,
                TenantStatus.ACTIVE,
                createdAt,
                createdAt,
                null);
    }

    private static TenantMembership membershipAt(
            UUID userId,
            UUID tenantId,
            String status,
            Instant joinedAt) {
        return new TenantMembership(
                UUID.randomUUID(),
                tenantId,
                userId,
                TenantInvitation.OWNER_ROLE,
                status,
                joinedAt,
                joinedAt,
                1);
    }

    private static TenantInvitation invitation(
            UUID tenantId,
            UUID invitedBy,
            String email,
            String tokenHash) {
        return new TenantInvitation(
                UUID.randomUUID(),
                tenantId,
                email,
                TenantInvitation.OWNER_ROLE,
                TenantInvitation.OWNER_PROVISIONING_PURPOSE,
                tokenHash,
                InvitationStatus.PENDING,
                invitedBy,
                null,
                NOW.plus(1, ChronoUnit.DAYS),
                NOW,
                null);
    }

    private static void await(CountDownLatch latch) {
        try {
            if (!latch.await(5, TimeUnit.SECONDS)) {
                throw new IllegalStateException("Timed out waiting for concurrent repository test");
            }
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Concurrent repository test was interrupted", exception);
        }
    }
}

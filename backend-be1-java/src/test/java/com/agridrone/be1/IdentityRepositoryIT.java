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
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.jdbc.core.JdbcTemplate;
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

    private void verifyTenantInvitation(User admin, User target, Tenant tenant) {
        TenantInvitation invitation = new TenantInvitation(
                UUID.randomUUID(),
                tenant.id(),
                target.email(),
                "a".repeat(64),
                InvitationStatus.PENDING,
                admin.id(),
                null,
                NOW.plus(1, ChronoUnit.DAYS),
                NOW,
                null);

        tenantInvitations.add(invitation);

        assertThat(tenantInvitations.findByTokenHashForUpdate(invitation.tokenHash()))
                .contains(invitation);
        assertThat(tenantInvitations.markAccepted(
                        invitation.id(), target.id(), NOW.plus(1, ChronoUnit.HOURS)))
                .isTrue();
        assertThat(tenantInvitations.markAccepted(
                        invitation.id(), target.id(), NOW.plus(2, ChronoUnit.HOURS)))
                .isFalse();
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
}

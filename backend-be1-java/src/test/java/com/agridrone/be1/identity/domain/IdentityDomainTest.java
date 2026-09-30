package com.agridrone.be1.identity.domain;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class IdentityDomainTest {
    private static final Instant NOW = Instant.parse("2026-09-28T00:00:00Z");

    @Test
    void newManagerStartsSuspendedUnqualifiedAndCannotBeAssigned() {
        SystemManagerProfile profile = SystemManagerProfile.create(UUID.randomUUID(), NOW);

        assertThat(profile.status()).isEqualTo(SystemManagerProfileStatus.SUSPENDED);
        assertThat(profile.availability()).isEqualTo(ManagerAvailability.UNAVAILABLE);
        assertThat(profile.canBeAssigned(NOW)).isFalse();
    }

    @Test
    void onlyActiveAvailableCurrentlyQualifiedManagerCanBeAssigned() {
        SystemManagerProfile profile = SystemManagerProfile.create(UUID.randomUUID(), NOW);
        profile.activate(NOW, 1);
        profile.updateQualification(
                QualificationStatus.QUALIFIED,
                NOW.plus(30, ChronoUnit.DAYS),
                NOW,
                2);
        profile.updateAvailability(ManagerAvailability.AVAILABLE, NOW, 3);

        assertThat(profile.canBeAssigned(NOW)).isTrue();
        assertThat(profile.canBeAssigned(NOW.plus(31, ChronoUnit.DAYS))).isFalse();
    }

    @Test
    void ownerOnlyMembershipAndOptimisticAssignmentAreEnforcedByDomain() {
        assertThatThrownBy(() -> new TenantMembership(
                        UUID.randomUUID(),
                        UUID.randomUUID(),
                        UUID.randomUUID(),
                        "MEMBER",
                        "ACTIVE",
                        NOW,
                        NOW,
                        1))
                .isInstanceOf(IllegalArgumentException.class);

        FarmManagerAssignment assignment = newAssignment();

        assertThatThrownBy(() -> assignment.end(UUID.randomUUID(), "done", NOW, 2))
                .isInstanceOf(IllegalStateException.class);
    }

    @Test
    void suspendingManagerAlsoMakesManagerUnavailable() {
        SystemManagerProfile profile = SystemManagerProfile.create(UUID.randomUUID(), NOW);
        profile.updateQualification(
                QualificationStatus.QUALIFIED,
                NOW.plus(30, ChronoUnit.DAYS),
                NOW,
                1);
        profile.activate(NOW, 2);
        profile.updateAvailability(ManagerAvailability.AVAILABLE, NOW, 3);

        profile.suspend(NOW.plus(1, ChronoUnit.MINUTES), 4);

        assertThat(profile.status()).isEqualTo(SystemManagerProfileStatus.SUSPENDED);
        assertThat(profile.availability()).isEqualTo(ManagerAvailability.UNAVAILABLE);
        assertThat(profile.canOperate(NOW.plus(1, ChronoUnit.MINUTES))).isFalse();
    }

    @Test
    void qualifiedManagerRequiresFutureQualificationExpiry() {
        SystemManagerProfile profile = SystemManagerProfile.create(UUID.randomUUID(), NOW);

        assertThatThrownBy(() -> profile.updateQualification(
                        QualificationStatus.QUALIFIED,
                        NOW,
                        NOW,
                        1))
                .isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void endingAssignmentPreservesHistoryActorReasonAndVersion() {
        UUID assignedBy = UUID.randomUUID();
        UUID endedBy = UUID.randomUUID();
        FarmManagerAssignment assignment = FarmManagerAssignment.create(
                UUID.randomUUID(),
                UUID.randomUUID(),
                UUID.randomUUID(),
                assignedBy,
                "initial",
                NOW);

        assignment.end(
                endedBy,
                "regional reassignment",
                NOW.plus(2, ChronoUnit.DAYS),
                1);

        assertThat(assignment.isActive()).isFalse();
        assertThat(assignment.assignedBy()).isEqualTo(assignedBy);
        assertThat(assignment.assignmentReason()).isEqualTo("initial");
        assertThat(assignment.endedBy()).isEqualTo(endedBy);
        assertThat(assignment.endReason()).isEqualTo("regional reassignment");
        assertThat(assignment.version()).isEqualTo(2);
    }

    @Test
    void endingAssignmentRequiresReason() {
        FarmManagerAssignment assignment = newAssignment();

        assertThatThrownBy(() -> assignment.end(UUID.randomUUID(), " ", NOW, 1))
                .isInstanceOf(IllegalArgumentException.class);
    }

    private static FarmManagerAssignment newAssignment() {
        return FarmManagerAssignment.create(
                UUID.randomUUID(),
                UUID.randomUUID(),
                UUID.randomUUID(),
                UUID.randomUUID(),
                "initial",
                NOW);
    }
}

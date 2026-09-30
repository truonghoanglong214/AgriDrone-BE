package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.FarmManagerAssignment;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "farm_manager_assignments", schema = "identity")
public class FarmManagerAssignmentJpaEntity {
    @Id
    private UUID id;

    @Column(name = "tenant_id", nullable = false)
    private UUID tenantId;

    @Column(name = "farm_id", nullable = false)
    private UUID farmId;

    @Column(name = "system_manager_profile_id", nullable = false)
    private UUID profileId;

    @Column(name = "assigned_by", nullable = false)
    private UUID assignedBy;

    @Column(name = "assignment_reason", nullable = false)
    private String assignmentReason;

    @Column(name = "assigned_at", nullable = false, updatable = false)
    private Instant assignedAt;

    @Column(name = "ended_by")
    private UUID endedBy;

    @Column(name = "end_reason")
    private String endReason;

    @Column(name = "ended_at")
    private Instant endedAt;

    @Version
    @Column(nullable = false)
    private long version;

    protected FarmManagerAssignmentJpaEntity() {}

    public FarmManagerAssignmentJpaEntity(FarmManagerAssignment assignment) {
        id = assignment.id();
        tenantId = assignment.tenantId();
        farmId = assignment.farmId();
        profileId = assignment.profileId();
        assignedBy = assignment.assignedBy();
        assignmentReason = assignment.assignmentReason();
        assignedAt = assignment.assignedAt();
        endedBy = assignment.endedBy();
        endReason = assignment.endReason();
        endedAt = assignment.endedAt();
        version = assignment.version();
    }

    public boolean applyEnd(FarmManagerAssignment assignment, long expectedVersion) {
        if (endedAt != null
                || version != expectedVersion
                || assignment.version() != expectedVersion + 1) {
            return false;
        }
        endedBy = assignment.endedBy();
        endReason = assignment.endReason();
        endedAt = assignment.endedAt();
        return true;
    }

    public FarmManagerAssignment toDomain() {
        return new FarmManagerAssignment(
                id,
                tenantId,
                farmId,
                profileId,
                assignedBy,
                assignmentReason,
                assignedAt,
                endedBy,
                endReason,
                endedAt,
                version);
    }
}

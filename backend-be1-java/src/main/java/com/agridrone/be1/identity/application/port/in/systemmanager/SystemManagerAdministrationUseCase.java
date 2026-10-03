package com.agridrone.be1.identity.application.port.in.systemmanager;

import com.agridrone.be1.identity.domain.ManagerAvailability;
import com.agridrone.be1.identity.domain.QualificationStatus;
import com.agridrone.be1.identity.domain.SystemManagerProfileStatus;
import java.time.Instant;
import java.util.UUID;

public interface SystemManagerAdministrationUseCase {
    ProfileResult createProfile(UUID userId);

    ProfileResult activate(UUID profileId, String reason, long expectedVersion);

    ProfileResult suspend(UUID profileId, String reason, long expectedVersion);

    ProfileResult updateAvailability(
            UUID profileId,
            ManagerAvailability availability,
            String reason,
            long expectedVersion);

    ProfileResult updateQualification(
            UUID profileId,
            QualificationStatus status,
            Instant expiresAt,
            String reason,
            long expectedVersion);

    AssignmentResult assignPrimary(
            UUID farmId,
            UUID profileId,
            String reason,
            Long expectedCurrentAssignmentVersion);

    void endPrimary(UUID farmId, String reason, long expectedVersion);

    record ProfileResult(
            UUID id,
            UUID userId,
            String email,
            String fullName,
            SystemManagerProfileStatus status,
            ManagerAvailability availability,
            QualificationStatus qualificationStatus,
            Instant qualificationExpiresAt,
            long version) {}

    record AssignmentResult(
            UUID assignmentId,
            UUID tenantId,
            UUID farmId,
            UUID systemManagerProfileId,
            UUID managerUserId,
            Instant assignedAt,
            long version) {}
}

package com.agridrone.be1.identity.application.service.systemmanager;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.SystemManagerErrorCodes;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAdministrationUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.FarmManagerAssignmentRepository;
import com.agridrone.be1.identity.application.port.out.persistence.RoleRepository;
import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerProfileRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.reference.FarmReferenceQuery;
import com.agridrone.be1.identity.domain.FarmManagerAssignment;
import com.agridrone.be1.identity.domain.ManagerAvailability;
import com.agridrone.be1.identity.domain.QualificationStatus;
import com.agridrone.be1.identity.domain.SystemManagerProfile;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.shared.audit.AuditEntry;
import com.agridrone.be1.shared.audit.AuditWriter;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.fasterxml.jackson.databind.node.JsonNodeFactory;
import java.time.Clock;
import java.time.Instant;
import java.util.Locale;
import java.util.Objects;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.http.HttpStatus;
import org.springframework.orm.ObjectOptimisticLockingFailureException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class SystemManagerAdministrationService
        implements SystemManagerAdministrationUseCase {
    private final UserRepository users;
    private final RoleRepository roles;
    private final SystemManagerProfileRepository profiles;
    private final FarmManagerAssignmentRepository assignments;
    private final FarmReferenceQuery farms;
    private final AuditWriter audit;
    private final Clock clock;
    private final TransactionTemplate transactions;

    public SystemManagerAdministrationService(
            UserRepository users,
            RoleRepository roles,
            SystemManagerProfileRepository profiles,
            FarmManagerAssignmentRepository assignments,
            FarmReferenceQuery farms,
            AuditWriter audit,
            Clock clock,
            TransactionTemplate transactions) {
        this.users = users;
        this.roles = roles;
        this.profiles = profiles;
        this.assignments = assignments;
        this.farms = farms;
        this.audit = audit;
        this.clock = clock;
        this.transactions = transactions;
    }

    @Override
    public ProfileResult createProfile(UUID userId) {
        try {
            return execute(() -> createProfileInTransaction(userId));
        } catch (DataIntegrityViolationException exception) {
            if (hasConstraint(exception, "uq_system_manager_profiles_user")) {
                throw profileAlreadyExists();
            }
            throw exception;
        }
    }

    private ProfileResult createProfileInTransaction(UUID userId) {
        requireSystemAdmin();
        User user = users.findById(userId).orElseThrow(() -> error(
                HttpStatus.NOT_FOUND,
                "User.NotFound",
                "The user was not found."));
        if (!user.isActive()) {
            throw error(
                    HttpStatus.CONFLICT,
                    SystemManagerErrorCodes.USER_MUST_BE_ACTIVE,
                    "A SystemManager profile can only be created for an active user.");
        }
        if (profiles.findByUserId(userId).isPresent()) {
            throw profileAlreadyExists();
        }
        var role = roles.findByCode(SystemRoleCodes.SYSTEM_MANAGER)
                .orElseThrow(SystemManagerAdministrationService::roleMissing);
        Instant now = clock.instant();
        SystemManagerProfile profile = SystemManagerProfile.create(user.id(), now);
        if (!users.findSystemRoleCodes(user.id()).contains(SystemRoleCodes.SYSTEM_MANAGER)) {
            users.assignSystemRole(user.id(), role.id());
        }
        profiles.save(profile);
        audit.append(new AuditEntry(
                null,
                "SystemManagerProfile",
                profile.id(),
                "CREATE",
                null,
                profileSnapshot(profile),
                null));
        return profileResult(profile, user);
    }

    @Override
    public ProfileResult activate(UUID profileId, String reason, long expectedVersion) {
        return mutateProfile(
                profileId, reason, expectedVersion, "ACTIVATE",
                (profile, now) -> profile.activate(now, expectedVersion));
    }

    @Override
    public ProfileResult suspend(UUID profileId, String reason, long expectedVersion) {
        return mutateProfile(
                profileId, reason, expectedVersion, "SUSPEND",
                (profile, now) -> profile.suspend(now, expectedVersion));
    }

    @Override
    public ProfileResult updateAvailability(
            UUID profileId,
            ManagerAvailability availability,
            String reason,
            long expectedVersion) {
        return mutateProfile(
                profileId, reason, expectedVersion, "UPDATE_AVAILABILITY",
                (profile, now) -> profile.updateAvailability(
                        availability, now, expectedVersion));
    }

    @Override
    public ProfileResult updateQualification(
            UUID profileId,
            QualificationStatus status,
            Instant expiresAt,
            String reason,
            long expectedVersion) {
        if (status == QualificationStatus.QUALIFIED
                && (expiresAt == null || !expiresAt.isAfter(clock.instant()))) {
            throw error(
                    HttpStatus.UNPROCESSABLE_ENTITY,
                    SystemManagerErrorCodes.INVALID_QUALIFICATION_EXPIRY,
                    "A qualified manager requires a qualification expiry in the future.");
        }
        return mutateProfile(
                profileId, reason, expectedVersion, "UPDATE_QUALIFICATION",
                (profile, now) -> profile.updateQualification(
                        status, expiresAt, now, expectedVersion));
    }

    private ProfileResult mutateProfile(
            UUID profileId,
            String reason,
            long expectedVersion,
            String action,
            ProfileMutation mutation) {
        try {
            return execute(() -> {
                requireSystemAdmin();
                SystemManagerProfile profile = profiles.findById(profileId)
                        .orElseThrow(SystemManagerAdministrationService::profileNotFound);
                if (profile.version() != expectedVersion) {
                    throw concurrentUpdate();
                }
                User user = users.findById(profile.userId())
                        .orElseThrow(SystemManagerAdministrationService::profileNotFound);
                var before = profileSnapshot(profile);
                mutation.apply(profile, clock.instant());
                profiles.save(profile);
                audit.append(new AuditEntry(
                        null,
                        "SystemManagerProfile",
                        profile.id(),
                        action,
                        before,
                        profileSnapshot(profile),
                        normalizeReason(reason)));
                return profileResult(profile, user);
            });
        } catch (ObjectOptimisticLockingFailureException exception) {
            throw concurrentUpdate();
        }
    }

    @Override
    public AssignmentResult assignPrimary(
            UUID farmId,
            UUID profileId,
            String reason,
            Long expectedCurrentAssignmentVersion) {
        try {
            return execute(() -> assignPrimaryInTransaction(
                    farmId, profileId, reason, expectedCurrentAssignmentVersion));
        } catch (DataIntegrityViolationException exception) {
            if (hasConstraint(exception, "uq_farm_manager_assignments_active_farm")) {
                throw error(
                        HttpStatus.CONFLICT,
                        SystemManagerErrorCodes.ACTIVE_ASSIGNMENT_CONFLICT,
                        "The Farm already has another active primary SystemManager assignment.");
            }
            throw exception;
        } catch (ObjectOptimisticLockingFailureException exception) {
            throw concurrentUpdate();
        }
    }

    private AssignmentResult assignPrimaryInTransaction(
            UUID farmId,
            UUID profileId,
            String reason,
            Long expectedCurrentAssignmentVersion) {
        UUID actorId = requireSystemAdmin();
        var farm = farms.findActiveById(farmId).orElseThrow(() -> error(
                HttpStatus.NOT_FOUND,
                SystemManagerErrorCodes.FARM_NOT_FOUND,
                "The active Farm was not found."));
        SystemManagerProfile profile = profiles.findById(profileId)
                .orElseThrow(SystemManagerAdministrationService::notAssignable);
        User user = users.findById(profile.userId()).orElse(null);
        Instant now = clock.instant();
        if (user == null || !user.isActive() || !profile.canBeAssigned(now)) {
            throw notAssignable();
        }

        FarmManagerAssignment current = assignments.findActiveByFarmId(farmId).orElse(null);
        if (current != null) {
            if (expectedCurrentAssignmentVersion == null
                    || current.version() != expectedCurrentAssignmentVersion) {
                throw concurrentUpdate();
            }
            if (current.profileId().equals(profile.id())) {
                return assignmentResult(current, profile.userId());
            }
            var before = assignmentSnapshot(current);
            current.end(
                    actorId,
                    "Reassigned: " + normalizeReason(reason),
                    now,
                    expectedCurrentAssignmentVersion);
            if (!assignments.end(current, expectedCurrentAssignmentVersion)) {
                throw concurrentUpdate();
            }
            audit.append(new AuditEntry(
                    current.tenantId(),
                    current.farmId(),
                    "FarmManagerAssignment",
                    current.id(),
                    "END_FOR_REASSIGNMENT",
                    before,
                    assignmentSnapshot(current),
                    normalizeReason(reason)));
        }

        FarmManagerAssignment assignment = FarmManagerAssignment.create(
                farm.tenantId(), farm.farmId(), profile.id(), actorId,
                normalizeReason(reason), now);
        assignments.add(assignment);
        audit.append(new AuditEntry(
                assignment.tenantId(),
                assignment.farmId(),
                "FarmManagerAssignment",
                assignment.id(),
                current == null ? "ASSIGN" : "REASSIGN",
                current == null ? null : assignmentSnapshot(current),
                assignmentSnapshot(assignment),
                normalizeReason(reason)));
        return assignmentResult(assignment, profile.userId());
    }

    @Override
    public void endPrimary(UUID farmId, String reason, long expectedVersion) {
        try {
            execute(() -> {
                UUID actorId = requireSystemAdmin();
                FarmManagerAssignment assignment = assignments.findActiveByFarmId(farmId)
                        .orElseThrow(() -> error(
                                HttpStatus.NOT_FOUND,
                                SystemManagerErrorCodes.ASSIGNMENT_NOT_FOUND,
                                "The Farm has no active primary SystemManager assignment."));
                if (assignment.version() != expectedVersion) {
                    throw concurrentUpdate();
                }
                var before = assignmentSnapshot(assignment);
                assignment.end(actorId, normalizeReason(reason), clock.instant(), expectedVersion);
                if (!assignments.end(assignment, expectedVersion)) {
                    throw concurrentUpdate();
                }
                audit.append(new AuditEntry(
                        assignment.tenantId(),
                        assignment.farmId(),
                        "FarmManagerAssignment",
                        assignment.id(),
                        "END",
                        before,
                        assignmentSnapshot(assignment),
                        normalizeReason(reason)));
                return Boolean.TRUE;
            });
        } catch (ObjectOptimisticLockingFailureException exception) {
            throw concurrentUpdate();
        }
    }

    private UUID requireSystemAdmin() {
        var context = ExecutionContext.require();
        if (context.actorId() == null) {
            throw error(
                    HttpStatus.UNAUTHORIZED,
                    AuthenticationErrorCodes.CURRENT_USER_REQUIRED,
                    "A current user is required.");
        }
        if (!context.roles().contains(SystemRoleCodes.SYSTEM_ADMIN)) {
            throw error(
                    HttpStatus.FORBIDDEN,
                    "Auth.AccessDenied",
                    "System Administrator access is required.");
        }
        return context.actorId();
    }

    private <T> T execute(java.util.function.Supplier<T> work) {
        return Objects.requireNonNull(transactions.execute(status -> work.get()));
    }

    private static ProfileResult profileResult(SystemManagerProfile profile, User user) {
        return new ProfileResult(
                profile.id(), profile.userId(), user.email(), user.fullName(),
                profile.status(), profile.availability(), profile.qualificationStatus(),
                profile.qualificationExpiresAt(), profile.version());
    }

    private static AssignmentResult assignmentResult(
            FarmManagerAssignment assignment,
            UUID managerUserId) {
        return new AssignmentResult(
                assignment.id(), assignment.tenantId(), assignment.farmId(),
                assignment.profileId(), managerUserId, assignment.assignedAt(),
                assignment.version());
    }

    private static com.fasterxml.jackson.databind.JsonNode profileSnapshot(
            SystemManagerProfile profile) {
        var node = JsonNodeFactory.instance.objectNode();
        node.put("userId", profile.userId().toString());
        node.put("status", profile.status().name());
        node.put("availability", profile.availability().name());
        node.put("qualificationStatus", profile.qualificationStatus().name());
        if (profile.qualificationExpiresAt() != null) {
            node.put("qualificationExpiresAt", profile.qualificationExpiresAt().toString());
        }
        node.put("version", profile.version());
        return node;
    }

    private static com.fasterxml.jackson.databind.JsonNode assignmentSnapshot(
            FarmManagerAssignment assignment) {
        var node = JsonNodeFactory.instance.objectNode();
        node.put("tenantId", assignment.tenantId().toString());
        node.put("farmId", assignment.farmId().toString());
        node.put("systemManagerProfileId", assignment.profileId().toString());
        node.put("assignedAt", assignment.assignedAt().toString());
        if (assignment.endedAt() != null) {
            node.put("endedAt", assignment.endedAt().toString());
        }
        node.put("version", assignment.version());
        return node;
    }

    private static String normalizeReason(String reason) {
        if (reason == null || reason.isBlank()) {
            throw new IllegalArgumentException("reason is required");
        }
        return reason.trim();
    }

    private static StableApiException profileNotFound() {
        return error(
                HttpStatus.NOT_FOUND,
                SystemManagerErrorCodes.PROFILE_NOT_FOUND,
                "The SystemManager profile was not found.");
    }

    private static StableApiException profileAlreadyExists() {
        return error(
                HttpStatus.CONFLICT,
                SystemManagerErrorCodes.PROFILE_ALREADY_EXISTS,
                "The user already has a SystemManager profile.");
    }

    private static StableApiException roleMissing() {
        return error(
                HttpStatus.INTERNAL_SERVER_ERROR,
                SystemManagerErrorCodes.ROLE_MISSING,
                "The SYSTEM_MANAGER role has not been seeded.");
    }

    private static StableApiException notAssignable() {
        return error(
                HttpStatus.CONFLICT,
                SystemManagerErrorCodes.NOT_ASSIGNABLE,
                "The manager must have an active profile, be available, and hold a non-expired flight qualification.");
    }

    private static StableApiException concurrentUpdate() {
        return error(
                HttpStatus.CONFLICT,
                SystemManagerErrorCodes.CONCURRENT_UPDATE,
                "The profile or assignment changed while the request was being processed. Reload it and try again.");
    }

    private static StableApiException error(HttpStatus status, String code, String message) {
        return new StableApiException(status, code, message);
    }

    private static boolean hasConstraint(Throwable exception, String constraint) {
        String expected = constraint.toLowerCase(Locale.ROOT);
        Throwable current = exception;
        while (current != null) {
            if (current.getMessage() != null
                    && current.getMessage().toLowerCase(Locale.ROOT).contains(expected)) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    @FunctionalInterface
    private interface ProfileMutation {
        void apply(SystemManagerProfile profile, Instant now);
    }
}

package com.agridrone.be1.identity.application.service.systemmanager;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.SystemManagerErrorCodes;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAccessDecisionUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.FarmManagerAssignmentRepository;
import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerProfileRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.reference.FarmReferenceQuery;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ExecutionContext;
import java.time.Clock;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class SystemManagerAccessDecisionService
        implements SystemManagerAccessDecisionUseCase {
    private final UserRepository users;
    private final SystemManagerProfileRepository profiles;
    private final FarmManagerAssignmentRepository assignments;
    private final FarmReferenceQuery farms;
    private final Clock clock;

    public SystemManagerAccessDecisionService(
            UserRepository users,
            SystemManagerProfileRepository profiles,
            FarmManagerAssignmentRepository assignments,
            FarmReferenceQuery farms,
            Clock clock) {
        this.users = users;
        this.profiles = profiles;
        this.assignments = assignments;
        this.farms = farms;
        this.clock = clock;
    }

    @Override
    @Transactional(readOnly = true)
    public FarmAccess requireFarmAccess(UUID farmId) {
        var context = ExecutionContext.require();
        UUID actorId = context.actorId();
        if (actorId == null) {
            throw new StableApiException(
                    HttpStatus.UNAUTHORIZED,
                    AuthenticationErrorCodes.CURRENT_USER_REQUIRED,
                    "A current user is required.");
        }
        if (!context.roles().contains(SystemRoleCodes.SYSTEM_MANAGER)) {
            throw accessDenied();
        }
        var farm = farms.findActiveById(farmId).orElseThrow(() -> new StableApiException(
                HttpStatus.NOT_FOUND,
                SystemManagerErrorCodes.FARM_NOT_FOUND,
                "The active Farm was not found."));
        var user = users.findById(actorId).orElse(null);
        var profile = profiles.findByUserId(actorId).orElse(null);
        if (user == null || !user.isActive()
                || profile == null || !profile.canOperate(clock.instant())) {
            throw new StableApiException(
                    HttpStatus.CONFLICT,
                    SystemManagerErrorCodes.NOT_ASSIGNABLE,
                    "The SystemManager profile is not active and flight-qualified.");
        }
        var assignment = assignments.findActiveByFarmId(farmId).orElse(null);
        if (assignment == null
                || !assignment.profileId().equals(profile.id())
                || !assignment.tenantId().equals(farm.tenantId())) {
            throw accessDenied();
        }
        return new FarmAccess(farm.tenantId(), farm.farmId(), profile.id());
    }

    private static StableApiException accessDenied() {
        return new StableApiException(
                HttpStatus.FORBIDDEN,
                SystemManagerErrorCodes.ACCESS_DENIED,
                "The SystemManager is not the active primary manager for this Farm.");
    }
}

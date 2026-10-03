package com.agridrone.be1.identity.application.service.systemmanager;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.SystemManagerErrorCodes;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerWorkUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.FarmManagerAssignmentRepository;
import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerProfileRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.reference.FarmReferenceQuery;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ExecutionContext;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.function.Function;
import java.util.stream.Collectors;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class SystemManagerWorkService implements SystemManagerWorkUseCase {
    private final UserRepository users;
    private final SystemManagerProfileRepository profiles;
    private final FarmManagerAssignmentRepository assignments;
    private final FarmReferenceQuery farms;
    private final Clock clock;

    public SystemManagerWorkService(
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
    public List<AssignedFarmResult> findAssignedFarms() {
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
        var user = users.findById(actorId).orElse(null);
        var profile = profiles.findByUserId(actorId).orElse(null);
        if (user == null || !user.isActive()
                || profile == null || !profile.canOperate(clock.instant())) {
            throw notAssignable();
        }
        var activeAssignments = assignments.findActiveByProfileId(profile.id());
        Map<UUID, com.agridrone.be1.identity.domain.FarmManagerAssignment> byFarm =
                activeAssignments.stream().collect(Collectors.toMap(
                        com.agridrone.be1.identity.domain.FarmManagerAssignment::farmId,
                        Function.identity()));
        return farms.findActiveByIds(byFarm.keySet()).stream()
                .filter(farm -> {
                    var assignment = byFarm.get(farm.farmId());
                    return assignment != null
                            && assignment.tenantId().equals(farm.tenantId());
                })
                .map(farm -> new AssignedFarmResult(
                        farm.tenantId(),
                        farm.farmId(),
                        farm.code(),
                        farm.name(),
                        farm.address(),
                        farm.areaHectares(),
                        byFarm.get(farm.farmId()).assignedAt()))
                .toList();
    }

    private static StableApiException notAssignable() {
        return new StableApiException(
                HttpStatus.CONFLICT,
                SystemManagerErrorCodes.NOT_ASSIGNABLE,
                "The manager must have an active profile and hold a non-expired flight qualification.");
    }

    private static StableApiException accessDenied() {
        return new StableApiException(
                HttpStatus.FORBIDDEN,
                SystemManagerErrorCodes.ACCESS_DENIED,
                "System Manager access is required.");
    }
}

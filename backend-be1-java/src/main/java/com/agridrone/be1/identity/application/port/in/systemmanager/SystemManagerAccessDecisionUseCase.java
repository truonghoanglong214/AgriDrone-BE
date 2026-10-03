package com.agridrone.be1.identity.application.port.in.systemmanager;

import java.util.UUID;

public interface SystemManagerAccessDecisionUseCase {
    FarmAccess requireFarmAccess(UUID farmId);

    record FarmAccess(UUID tenantId, UUID farmId, UUID profileId) {}
}

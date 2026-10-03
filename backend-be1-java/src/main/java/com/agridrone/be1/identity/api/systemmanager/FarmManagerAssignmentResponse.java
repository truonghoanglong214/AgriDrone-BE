package com.agridrone.be1.identity.api.systemmanager;

import java.time.Instant;
import java.util.UUID;

public record FarmManagerAssignmentResponse(
        UUID assignmentId,
        UUID tenantId,
        UUID farmId,
        UUID systemManagerProfileId,
        UUID managerUserId,
        Instant assignedAt,
        long version) {}

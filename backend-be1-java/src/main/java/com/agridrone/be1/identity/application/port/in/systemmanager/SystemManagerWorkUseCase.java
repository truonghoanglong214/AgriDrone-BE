package com.agridrone.be1.identity.application.port.in.systemmanager;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

public interface SystemManagerWorkUseCase {
    List<AssignedFarmResult> findAssignedFarms();

    record AssignedFarmResult(
            UUID tenantId,
            UUID farmId,
            String code,
            String name,
            String address,
            BigDecimal areaHectares,
            Instant assignedAt) {}
}

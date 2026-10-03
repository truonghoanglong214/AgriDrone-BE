package com.agridrone.be1.identity.api.systemmanager;

import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerWorkUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import io.swagger.v3.oas.annotations.tags.Tag;
import java.util.List;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@Tag(name = OpenApiTags.SYSTEM_MANAGER_WORKSPACE,
        description = OpenApiTags.SYSTEM_MANAGER_WORKSPACE_DESCRIPTION)
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
@RequestMapping("/api/system-manager")
@PreAuthorize("hasAuthority('SYSTEM_MANAGER')")
public class SystemManagerWorkController {
    private final SystemManagerWorkUseCase work;

    public SystemManagerWorkController(SystemManagerWorkUseCase work) {
        this.work = work;
    }

    @GetMapping("/farms")
    public ResponseEntity<List<AssignedFarmResponse>> assignedFarms() {
        return ResponseEntity.ok(work.findAssignedFarms().stream()
                .map(result -> new AssignedFarmResponse(
                        result.tenantId(), result.farmId(), result.code(), result.name(),
                        result.address(), result.areaHectares(), result.assignedAt()))
                .toList());
    }
}

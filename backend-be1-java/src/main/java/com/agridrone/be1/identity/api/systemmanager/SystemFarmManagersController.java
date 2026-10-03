package com.agridrone.be1.identity.api.systemmanager;

import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAdministrationUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@Tag(name = OpenApiTags.SYSTEM_MANAGER_ADMINISTRATION,
        description = OpenApiTags.SYSTEM_MANAGER_ADMINISTRATION_DESCRIPTION)
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
@RequestMapping("/api/system/farms/{farmId}/primary-manager")
@PreAuthorize("hasAuthority('SYSTEM_ADMIN')")
public class SystemFarmManagersController {
    private final SystemManagerAdministrationUseCase administration;

    public SystemFarmManagersController(SystemManagerAdministrationUseCase administration) {
        this.administration = administration;
    }

    @PutMapping
    public ResponseEntity<FarmManagerAssignmentResponse> assign(
            @PathVariable UUID farmId,
            @Valid @RequestBody AssignPrimaryFarmManagerRequest request) {
        var result = administration.assignPrimary(
                farmId,
                request.systemManagerProfileId(),
                request.reason(),
                request.expectedCurrentAssignmentVersion());
        return ResponseEntity.ok(new FarmManagerAssignmentResponse(
                result.assignmentId(), result.tenantId(), result.farmId(),
                result.systemManagerProfileId(), result.managerUserId(),
                result.assignedAt(), result.version()));
    }

    @PutMapping("/end")
    public ResponseEntity<Void> end(
            @PathVariable UUID farmId,
            @Valid @RequestBody EndPrimaryFarmManagerAssignmentRequest request) {
        administration.endPrimary(farmId, request.reason(), request.expectedVersion());
        return ResponseEntity.noContent().build();
    }
}

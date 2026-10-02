package com.agridrone.be1.identity.api.tenant;

import static com.agridrone.be1.identity.api.IdentityWireValues.membershipStatus;
import static com.agridrone.be1.identity.api.IdentityWireValues.role;

import com.agridrone.be1.identity.application.port.in.tenantquery.TenantQueryUseCase;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import java.util.UUID;
import org.springframework.http.ResponseEntity;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@Validated
@RestController
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
@RequestMapping("/api/system/users")
@PreAuthorize("hasAuthority('SYSTEM_ADMIN')")
public class SystemUserTenantsController {
    private final TenantQueryUseCase queries;

    public SystemUserTenantsController(TenantQueryUseCase queries) {
        this.queries = queries;
    }

    @GetMapping("/{userId}/tenants")
    public ResponseEntity<PageResponse<UserTenantListItemResponse>> findUserTenants(
            @PathVariable UUID userId,
            @RequestParam(defaultValue = "1") @Min(1) int pageNumber,
            @RequestParam(defaultValue = "20") @Min(1) @Max(100) int pageSize) {
        var result = queries.findUserTenants(userId, new PageRequest(pageNumber, pageSize))
                .map(item -> new UserTenantListItemResponse(
                        item.id(),
                        item.tenantId(),
                        role(item.role()),
                        membershipStatus(item.status()),
                        item.joinedAt(),
                        item.createdAt()));
        return ResponseEntity.ok(result);
    }
}

package com.agridrone.be1.identity.api.tenant;

import static com.agridrone.be1.identity.api.IdentityWireValues.tenantStatus;

import com.agridrone.be1.identity.application.port.in.tenantadmin.TenantAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantinvitation.TenantInvitationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantquery.TenantQueryUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import java.util.UUID;
import org.springframework.http.ResponseEntity;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@Validated
@RestController
@Tag(name = OpenApiTags.TENANT_ADMINISTRATION,
        description = OpenApiTags.TENANT_ADMINISTRATION_DESCRIPTION)
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
@RequestMapping("/api/system/tenants")
@PreAuthorize("hasAuthority('SYSTEM_ADMIN')")
public class SystemTenantsController {
    private final TenantAdministrationUseCase administration;
    private final TenantInvitationUseCase invitations;
    private final TenantQueryUseCase queries;

    public SystemTenantsController(
            TenantAdministrationUseCase administration,
            TenantInvitationUseCase invitations,
            TenantQueryUseCase queries) {
        this.administration = administration;
        this.invitations = invitations;
        this.queries = queries;
    }

    @PostMapping
    public ResponseEntity<CreateTenantResponse> create(
            @Valid @RequestBody CreateTenantRequest request) {
        var result = administration.create(new TenantAdministrationUseCase.CreateTenantCommand(
                request.tenantCode(), request.tenantName()));
        return ResponseEntity.status(201).body(new CreateTenantResponse(
                result.tenantId(),
                result.code(),
                result.name(),
                tenantStatus(result.status()),
                result.createdAt()));
    }

    @PutMapping("/{tenantId}/activate")
    public ResponseEntity<Void> activate(@PathVariable UUID tenantId) {
        administration.activate(tenantId);
        return ResponseEntity.noContent().build();
    }

    @PutMapping("/{tenantId}/deactivate")
    public ResponseEntity<Void> deactivate(@PathVariable UUID tenantId) {
        administration.deactivate(tenantId);
        return ResponseEntity.noContent().build();
    }

    @PostMapping("/{tenantId}/owner-provisionings")
    @Operation(tags = OpenApiTags.INVITATIONS)
    public ResponseEntity<ProvisionTenantOwnerResponse> provisionOwner(
            @PathVariable UUID tenantId,
            @Valid @RequestBody ProvisionTenantOwnerRequest request) {
        var result = invitations.provisionOwner(tenantId, request.email());
        return ResponseEntity.status(201).body(new ProvisionTenantOwnerResponse(
                result.invitationId(), result.email(), result.expiresAt()));
    }

    @GetMapping("/all")
    public ResponseEntity<PageResponse<TenantListItemResponse>> findAll(
            @RequestParam(defaultValue = "1") @Min(1) int pageNumber,
            @RequestParam(defaultValue = "20") @Min(1) @Max(100) int pageSize) {
        var result = queries.findTenants(new PageRequest(pageNumber, pageSize))
                .map(item -> new TenantListItemResponse(
                        item.id(),
                        item.code(),
                        item.name(),
                        tenantStatus(item.status()),
                        item.createdAt()));
        return ResponseEntity.ok(result);
    }
}

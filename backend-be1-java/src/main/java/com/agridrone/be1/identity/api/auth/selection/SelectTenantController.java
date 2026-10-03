package com.agridrone.be1.identity.api.auth.selection;

import com.agridrone.be1.identity.api.auth.login.LoginResponse;
import com.agridrone.be1.identity.application.port.in.selecttenant.SelectTenantUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

@RestController
@Tag(name = OpenApiTags.AUTHENTICATION,
        description = OpenApiTags.AUTHENTICATION_DESCRIPTION)
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public class SelectTenantController {
    private final SelectTenantUseCase selectTenant;

    public SelectTenantController(SelectTenantUseCase selectTenant) {
        this.selectTenant = selectTenant;
    }

    @PostMapping("/api/auth/select-tenant")
    public ResponseEntity<LoginResponse> select(@Valid @RequestBody SelectTenantRequest request) {
        var result = selectTenant.select(new SelectTenantUseCase.SelectTenantCommand(
                request.selectionToken(), request.tenantId()));
        return ResponseEntity.ok(LoginResponse.from(result));
    }
}

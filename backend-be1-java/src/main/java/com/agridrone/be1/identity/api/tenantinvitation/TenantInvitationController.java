package com.agridrone.be1.identity.api.tenantinvitation;

import static com.agridrone.be1.identity.api.IdentityWireValues.role;

import com.agridrone.be1.identity.application.port.in.tenantinvitation.TenantInvitationUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

@RestController
@Tag(name = OpenApiTags.INVITATIONS,
        description = OpenApiTags.INVITATIONS_DESCRIPTION)
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class TenantInvitationController {
    private final TenantInvitationUseCase invitations;

    public TenantInvitationController(TenantInvitationUseCase invitations) {
        this.invitations = invitations;
    }

    @PostMapping("/api/auth/invitations/preview")
    public ResponseEntity<PreviewTenantInvitationResponse> preview(
            @Valid @RequestBody PreviewTenantInvitationRequest request) {
        var result = invitations.preview(request.token());
        return ResponseEntity.ok(new PreviewTenantInvitationResponse(
                result.maskedEmail(),
                result.tenantName(),
                role(result.role()),
                result.expiresAt(),
                result.requiresAccountCreation()));
    }

    @PostMapping("/invitations/accept")
    public ResponseEntity<AcceptTenantInvitationResponse> accept(
            @Valid @RequestBody AcceptTenantInvitationRequest request) {
        var result = invitations.accept(new TenantInvitationUseCase.AcceptCommand(
                request.token(), request.password(), request.fullName(), request.phone()));
        return ResponseEntity.ok(new AcceptTenantInvitationResponse(
                result.userId(),
                result.tenantId(),
                role(result.role()),
                result.accountCreated()));
    }
}

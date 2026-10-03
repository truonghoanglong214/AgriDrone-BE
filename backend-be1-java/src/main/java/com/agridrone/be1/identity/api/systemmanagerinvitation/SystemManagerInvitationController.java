package com.agridrone.be1.identity.api.systemmanagerinvitation;

import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerInvitationUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@Tag(name = OpenApiTags.INVITATIONS,
        description = OpenApiTags.INVITATIONS_DESCRIPTION)
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
@RequestMapping("/api/auth/system-manager-invitations")
public class SystemManagerInvitationController {
    private final SystemManagerInvitationUseCase invitations;

    public SystemManagerInvitationController(SystemManagerInvitationUseCase invitations) {
        this.invitations = invitations;
    }

    @PostMapping("/preview")
    public ResponseEntity<PreviewSystemManagerInvitationResponse> preview(
            @Valid @RequestBody PreviewSystemManagerInvitationRequest request) {
        var result = invitations.preview(request.token());
        return ResponseEntity.ok(new PreviewSystemManagerInvitationResponse(
                result.maskedEmail(), result.role(), result.expiresAt(),
                result.requiresAccountCreation()));
    }

    @PostMapping("/accept")
    public ResponseEntity<AcceptSystemManagerInvitationResponse> accept(
            @Valid @RequestBody AcceptSystemManagerInvitationRequest request) {
        var result = invitations.accept(new SystemManagerInvitationUseCase.AcceptCommand(
                request.token(), request.password(), request.fullName(), request.phone()));
        return ResponseEntity.ok(new AcceptSystemManagerInvitationResponse(
                result.userId(), result.profileId(), result.accountCreated()));
    }
}

package com.agridrone.be1.identity.api.systemmanager;

import static com.agridrone.be1.identity.api.IdentityWireValues.managerAvailability;
import static com.agridrone.be1.identity.api.IdentityWireValues.qualificationStatus;
import static com.agridrone.be1.identity.api.IdentityWireValues.systemManagerProfileStatus;

import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerInvitationUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
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
@RequestMapping("/api/system/managers")
@PreAuthorize("hasAuthority('SYSTEM_ADMIN')")
public class SystemManagersController {
    private final SystemManagerAdministrationUseCase administration;
    private final SystemManagerInvitationUseCase invitations;

    public SystemManagersController(
            SystemManagerAdministrationUseCase administration,
            SystemManagerInvitationUseCase invitations) {
        this.administration = administration;
        this.invitations = invitations;
    }

    @PostMapping
    public ResponseEntity<SystemManagerProfileResponse> create(
            @Valid @RequestBody CreateSystemManagerProfileRequest request) {
        return ResponseEntity.status(201).body(map(administration.createProfile(request.userId())));
    }

    @PutMapping("/{profileId}/activate")
    public ResponseEntity<SystemManagerProfileResponse> activate(
            @PathVariable UUID profileId,
            @Valid @RequestBody SystemManagerStateChangeRequest request) {
        return ResponseEntity.ok(map(administration.activate(
                profileId, request.reason(), request.expectedVersion())));
    }

    @PutMapping("/{profileId}/suspend")
    public ResponseEntity<SystemManagerProfileResponse> suspend(
            @PathVariable UUID profileId,
            @Valid @RequestBody SystemManagerStateChangeRequest request) {
        return ResponseEntity.ok(map(administration.suspend(
                profileId, request.reason(), request.expectedVersion())));
    }

    @PutMapping("/{profileId}/availability")
    public ResponseEntity<SystemManagerProfileResponse> updateAvailability(
            @PathVariable UUID profileId,
            @Valid @RequestBody UpdateSystemManagerAvailabilityRequest request) {
        return ResponseEntity.ok(map(administration.updateAvailability(
                profileId,
                managerAvailability(request.availability()),
                request.reason(),
                request.expectedVersion())));
    }

    @PutMapping("/{profileId}/qualification")
    public ResponseEntity<SystemManagerProfileResponse> updateQualification(
            @PathVariable UUID profileId,
            @Valid @RequestBody UpdateSystemManagerQualificationRequest request) {
        return ResponseEntity.ok(map(administration.updateQualification(
                profileId,
                qualificationStatus(request.status()),
                request.expiresAt(),
                request.reason(),
                request.expectedVersion())));
    }

    @PostMapping("/invitations")
    @Operation(tags = OpenApiTags.INVITATIONS)
    public ResponseEntity<InviteSystemManagerResponse> invite(
            @Valid @RequestBody InviteSystemManagerRequest request) {
        var result = invitations.invite(request.email());
        return ResponseEntity.status(201).body(new InviteSystemManagerResponse(
                result.invitationId(), result.email(), result.expiresAt(), result.emailSent()));
    }

    static SystemManagerProfileResponse map(
            SystemManagerAdministrationUseCase.ProfileResult result) {
        return new SystemManagerProfileResponse(
                result.id(), result.userId(), result.email(), result.fullName(),
                systemManagerProfileStatus(result.status()),
                managerAvailability(result.availability()),
                qualificationStatus(result.qualificationStatus()),
                result.qualificationExpiresAt(), result.version());
    }
}

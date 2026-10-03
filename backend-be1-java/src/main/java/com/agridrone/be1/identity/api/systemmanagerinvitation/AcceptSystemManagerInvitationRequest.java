package com.agridrone.be1.identity.api.systemmanagerinvitation;

import jakarta.validation.constraints.NotBlank;

public record AcceptSystemManagerInvitationRequest(
        @NotBlank String token,
        String password,
        String fullName,
        String phone) {}

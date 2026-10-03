package com.agridrone.be1.identity.api.systemmanagerinvitation;

import jakarta.validation.constraints.NotBlank;

public record PreviewSystemManagerInvitationRequest(@NotBlank String token) {}

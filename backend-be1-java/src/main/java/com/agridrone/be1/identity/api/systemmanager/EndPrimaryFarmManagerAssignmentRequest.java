package com.agridrone.be1.identity.api.systemmanager;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record EndPrimaryFarmManagerAssignmentRequest(
        @NotBlank @Size(max = 1000) String reason,
        @Min(1) long expectedVersion) {}

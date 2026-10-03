package com.agridrone.be1.identity.api.systemmanager;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.time.Instant;

public record UpdateSystemManagerQualificationRequest(
        @NotNull @Min(0) @Max(3) Integer status,
        Instant expiresAt,
        @NotBlank @Size(max = 1000) String reason,
        @Min(1) long expectedVersion) {}

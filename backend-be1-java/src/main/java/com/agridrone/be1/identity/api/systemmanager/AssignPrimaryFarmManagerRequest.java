package com.agridrone.be1.identity.api.systemmanager;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.util.UUID;

public record AssignPrimaryFarmManagerRequest(
        @NotNull UUID systemManagerProfileId,
        @NotBlank @Size(max = 1000) String reason,
        @Min(1) Long expectedCurrentAssignmentVersion) {}

package com.agridrone.be1.farm.api.contract;

import jakarta.validation.constraints.Positive;

public record ArchiveZoneRequest(
        @Positive(message = "Expected version must be greater than zero.")
        long expectedVersion) {}

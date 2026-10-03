package com.agridrone.be1.farm.api.contract;

import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Digits;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;
import java.math.BigDecimal;

public record UpdateZoneRequest(
        @NotBlank(message = "Zone name is required.")
        @Size(max = 100, message = "Zone name must not exceed 100 characters.")
        String name,
        @Valid GeoJsonPolygonRequest boundary,
        @DecimalMin(value = "0", inclusive = true,
                message = "Zone area must be greater than or equal to zero.")
        @Digits(integer = 8, fraction = 4,
                message = "Zone area must fit numeric(12,4).")
        BigDecimal areaHectares,
        @Positive(message = "Expected version must be greater than zero.")
        long expectedVersion) {}

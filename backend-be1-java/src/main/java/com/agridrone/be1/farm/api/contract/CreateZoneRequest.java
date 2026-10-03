package com.agridrone.be1.farm.api.contract;

import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Digits;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;
import java.math.BigDecimal;

public record CreateZoneRequest(
        @NotBlank(message = "Zone code is required.")
        @Size(max = 30, message = "Zone code must not exceed 30 characters.")
        @Pattern(regexp = "^[A-Za-z][A-Za-z0-9_]*$",
                message = "Zone code may contain letters, numbers and underscores.")
        String code,
        @NotBlank(message = "Zone name is required.")
        @Size(max = 100, message = "Zone name must not exceed 100 characters.")
        String name,
        @Valid GeoJsonPolygonRequest boundary,
        @DecimalMin(value = "0", inclusive = true,
                message = "Zone area must be greater than or equal to zero.")
        @Digits(integer = 8, fraction = 4,
                message = "Zone area must fit numeric(12,4).")
        BigDecimal areaHectares) {}

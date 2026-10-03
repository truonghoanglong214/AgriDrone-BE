package com.agridrone.be1.farm.api.contract;

import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Digits;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;
import java.math.BigDecimal;

public record UpdateFarmRequest(
        @NotBlank(message = "Farm name is required.")
        @Size(max = 150, message = "Farm name must not exceed 150 characters.")
        String name,
        @Size(max = 200, message = "Address must not exceed 200 characters.")
        String address,
        @Valid GeoJsonPolygonRequest boundary,
        @Valid GeoJsonPointRequest centerPoint,
        @DecimalMin(value = "0", inclusive = true,
                message = "Farm area must be greater than or equal to zero.")
        @Digits(integer = 8, fraction = 4,
                message = "Farm area must fit numeric(12,4).")
        BigDecimal areaHectares,
        @Positive(message = "Expected version must be greater than zero.")
        long expectedVersion) {}

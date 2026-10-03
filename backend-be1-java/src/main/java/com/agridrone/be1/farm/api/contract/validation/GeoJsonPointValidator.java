package com.agridrone.be1.farm.api.contract.validation;

import com.agridrone.be1.farm.api.contract.GeoJsonPointRequest;
import jakarta.validation.ConstraintValidator;
import jakarta.validation.ConstraintValidatorContext;

public final class GeoJsonPointValidator
        implements ConstraintValidator<ValidGeoJsonPoint, GeoJsonPointRequest> {
    @Override
    public boolean isValid(
            GeoJsonPointRequest value,
            ConstraintValidatorContext context) {
        if (value == null || !"Point".equalsIgnoreCase(value.type())) {
            return false;
        }
        double[] coordinates = value.coordinates();
        if (coordinates == null || coordinates.length != 2) {
            return false;
        }
        double longitude = coordinates[0];
        double latitude = coordinates[1];
        return Double.isFinite(longitude)
                && Double.isFinite(latitude)
                && longitude >= -180
                && longitude <= 180
                && latitude >= -90
                && latitude <= 90;
    }
}

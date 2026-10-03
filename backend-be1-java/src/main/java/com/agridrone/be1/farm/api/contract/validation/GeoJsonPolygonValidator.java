package com.agridrone.be1.farm.api.contract.validation;

import com.agridrone.be1.farm.api.contract.GeoJsonPolygonRequest;
import jakarta.validation.ConstraintValidator;
import jakarta.validation.ConstraintValidatorContext;

public final class GeoJsonPolygonValidator
        implements ConstraintValidator<ValidGeoJsonPolygon, GeoJsonPolygonRequest> {
    @Override
    public boolean isValid(
            GeoJsonPolygonRequest value,
            ConstraintValidatorContext context) {
        if (value == null || !"Polygon".equalsIgnoreCase(value.type())) {
            return false;
        }
        double[][][] rings = value.coordinates();
        if (rings == null || rings.length == 0) {
            return false;
        }
        for (double[][] ring : rings) {
            if (!isValidRing(ring)) {
                return false;
            }
        }
        return true;
    }

    private static boolean isValidRing(double[][] ring) {
        if (ring == null || ring.length < 4) {
            return false;
        }
        for (double[] position : ring) {
            if (!isValidPosition(position)) {
                return false;
            }
        }
        double[] first = ring[0];
        double[] last = ring[ring.length - 1];
        return Double.compare(first[0], last[0]) == 0
                && Double.compare(first[1], last[1]) == 0;
    }

    private static boolean isValidPosition(double[] position) {
        if (position == null || position.length != 2) {
            return false;
        }
        double longitude = position[0];
        double latitude = position[1];
        return Double.isFinite(longitude)
                && Double.isFinite(latitude)
                && longitude >= -180
                && longitude <= 180
                && latitude >= -90
                && latitude <= 90;
    }
}

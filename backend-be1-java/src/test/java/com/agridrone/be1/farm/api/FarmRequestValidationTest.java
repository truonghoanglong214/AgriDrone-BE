package com.agridrone.be1.farm.api;

import static org.assertj.core.api.Assertions.assertThat;

import com.agridrone.be1.farm.api.contract.ArchiveFarmRequest;
import com.agridrone.be1.farm.api.contract.ArchiveZoneRequest;
import com.agridrone.be1.farm.api.contract.CreateZoneRequest;
import com.agridrone.be1.farm.api.contract.GeoJsonPointRequest;
import com.agridrone.be1.farm.api.contract.GeoJsonPolygonRequest;
import com.agridrone.be1.farm.api.contract.UpdateFarmRequest;
import com.agridrone.be1.farm.api.contract.UpdateZoneRequest;
import jakarta.validation.ConstraintViolation;
import jakarta.validation.Validation;
import jakarta.validation.Validator;
import java.math.BigDecimal;
import java.util.Set;
import org.junit.jupiter.api.Test;

class FarmRequestValidationTest {

    private static final Validator VALIDATOR = Validation
            .buildDefaultValidatorFactory()
            .getValidator();

    @Test
    void validMutationRequestsHaveNoViolations() {
        UpdateFarmRequest farm = new UpdateFarmRequest(
                "Farm A", null, polygon(), point(), BigDecimal.ZERO, 1);
        CreateZoneRequest createZone = new CreateZoneRequest(
                "ZONE_A1", "Zone A", polygon(), BigDecimal.ZERO);
        UpdateZoneRequest updateZone = new UpdateZoneRequest(
                "Zone A", polygon(), BigDecimal.ZERO, 1);

        assertThat(VALIDATOR.validate(farm)).isEmpty();
        assertThat(VALIDATOR.validate(createZone)).isEmpty();
        assertThat(VALIDATOR.validate(updateZone)).isEmpty();
        assertThat(VALIDATOR.validate(new ArchiveFarmRequest(1))).isEmpty();
        assertThat(VALIDATOR.validate(new ArchiveZoneRequest(1))).isEmpty();
    }

    @Test
    void invalidFarmFieldsAreRejectedAtApiBoundary() {
        UpdateFarmRequest request = new UpdateFarmRequest(
                " ", "x".repeat(201), polygon(), point(),
                new BigDecimal("-0.01"), 0);

        assertThat(paths(VALIDATOR.validate(request)))
                .contains("name", "address", "areaHectares", "expectedVersion");
    }

    @Test
    void invalidZoneFieldsAreRejectedAtApiBoundary() {
        CreateZoneRequest create = new CreateZoneRequest(
                "1 invalid", " ", polygon(), new BigDecimal("-1"));
        UpdateZoneRequest update = new UpdateZoneRequest(
                "x".repeat(101), polygon(), BigDecimal.ZERO, 0);

        assertThat(paths(VALIDATOR.validate(create)))
                .contains("code", "name", "areaHectares");
        assertThat(paths(VALIDATOR.validate(update)))
                .contains("name", "expectedVersion");
    }

    @Test
    void areaPrecisionIsLockedToNumericTwelveFour() {
        UpdateFarmRequest tooManyIntegerDigits = new UpdateFarmRequest(
                "Farm A", null, null, null, new BigDecimal("123456789.0000"), 1);
        CreateZoneRequest tooManyFractionDigits = new CreateZoneRequest(
                "ZONE_A", "Zone A", null, new BigDecimal("1.00001"));

        assertThat(paths(VALIDATOR.validate(tooManyIntegerDigits)))
                .contains("areaHectares");
        assertThat(paths(VALIDATOR.validate(tooManyFractionDigits)))
                .contains("areaHectares");
    }

    @Test
    void invalidPointStructuresAreRejected() {
        assertThat(VALIDATOR.validate(new GeoJsonPointRequest(
                "LineString", new double[]{106.1, 10.1}))).isNotEmpty();
        assertThat(VALIDATOR.validate(new GeoJsonPointRequest(
                "Point", new double[]{106.1}))).isNotEmpty();
        assertThat(VALIDATOR.validate(new GeoJsonPointRequest(
                "Point", new double[]{181, 10.1}))).isNotEmpty();
        assertThat(VALIDATOR.validate(new GeoJsonPointRequest(
                "Point", new double[]{106.1, Double.NaN}))).isNotEmpty();
    }

    @Test
    void invalidPolygonStructuresAreRejected() {
        assertThat(VALIDATOR.validate(new GeoJsonPolygonRequest(
                "Point", polygon().coordinates()))).isNotEmpty();
        assertThat(VALIDATOR.validate(new GeoJsonPolygonRequest(
                "Polygon", new double[0][][]))).isNotEmpty();
        assertThat(VALIDATOR.validate(new GeoJsonPolygonRequest(
                "Polygon", new double[][][]{{{106, 10}, {107, 10}, {106, 10}}})))
                .isNotEmpty();
        assertThat(VALIDATOR.validate(new GeoJsonPolygonRequest(
                "Polygon", new double[][][]{{
                    {106, 10}, {107, 10}, {107, 11}, {106, 11}
                }}))).isNotEmpty();
        assertThat(VALIDATOR.validate(new GeoJsonPolygonRequest(
                "Polygon", new double[][][]{{
                    {106, 10}, {181, 10}, {107, 11}, {106, 10}
                }}))).isNotEmpty();
    }

    @Test
    void nestedInvalidGeoJsonIsReportedOnRequestField() {
        UpdateFarmRequest request = new UpdateFarmRequest(
                "Farm A",
                null,
                new GeoJsonPolygonRequest("Polygon", new double[0][][]),
                new GeoJsonPointRequest("Point", new double[]{106.1}),
                null,
                1);

        assertThat(paths(VALIDATOR.validate(request)))
                .anyMatch(path -> path.startsWith("boundary"))
                .anyMatch(path -> path.startsWith("centerPoint"));
    }

    private static Set<String> paths(Set<? extends ConstraintViolation<?>> violations) {
        return violations.stream()
                .map(violation -> violation.getPropertyPath().toString())
                .collect(java.util.stream.Collectors.toSet());
    }

    private static GeoJsonPointRequest point() {
        return new GeoJsonPointRequest("Point", new double[]{106.1, 10.1});
    }

    private static GeoJsonPolygonRequest polygon() {
        return new GeoJsonPolygonRequest("Polygon", new double[][][]{
            {{106.0, 10.0}, {106.2, 10.0}, {106.2, 10.2},
                    {106.0, 10.2}, {106.0, 10.0}}
        });
    }
}

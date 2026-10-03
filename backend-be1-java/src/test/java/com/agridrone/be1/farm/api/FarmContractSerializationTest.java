package com.agridrone.be1.farm.api;

import static org.assertj.core.api.Assertions.assertThat;

import com.agridrone.be1.farm.api.contract.ArchivedFarmResponse;
import com.agridrone.be1.farm.api.contract.CreateZoneRequest;
import com.agridrone.be1.farm.api.contract.CreateZoneResponse;
import com.agridrone.be1.farm.api.contract.FarmDetailResponse;
import com.agridrone.be1.farm.api.contract.FarmListItemResponse;
import com.agridrone.be1.farm.api.contract.GeoJsonPointRequest;
import com.agridrone.be1.farm.api.contract.GeoJsonPointResponse;
import com.agridrone.be1.farm.api.contract.GeoJsonPolygonRequest;
import com.agridrone.be1.farm.api.contract.GeoJsonPolygonResponse;
import com.agridrone.be1.farm.api.contract.UpdateFarmRequest;
import com.agridrone.be1.farm.api.contract.UpdateFarmResponse;
import com.agridrone.be1.farm.api.contract.UpdateZoneRequest;
import com.agridrone.be1.farm.api.contract.UpdateZoneResponse;
import com.agridrone.be1.farm.api.contract.ZoneDetailResponse;
import com.agridrone.be1.farm.api.contract.ZoneListItemResponse;
import com.agridrone.be1.farm.application.error.FarmErrorCodes;
import com.agridrone.be1.farm.application.error.FarmZoneErrorCodes;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import com.agridrone.be1.shared.error.ApiErrorResponse;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import java.io.IOException;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class FarmContractSerializationTest {

    private static final UUID TENANT_ID = UUID.fromString(
            "10000000-0000-0000-0000-000000000001");
    private static final UUID FARM_ID = UUID.fromString(
            "20000000-0000-0000-0000-000000000001");
    private static final UUID ZONE_ID = UUID.fromString(
            "30000000-0000-0000-0000-000000000001");
    private static final UUID CREATED_BY = UUID.fromString(
            "50000000-0000-0000-0000-000000000001");
    private static final Instant CREATED_AT = Instant.parse("2026-10-03T00:00:00Z");
    private static final Instant UPDATED_AT = Instant.parse("2026-10-03T01:00:00Z");
    private static final ObjectMapper JSON = new ObjectMapper()
            .findAndRegisterModules()
            .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
            .enable(DeserializationFeature.USE_BIG_DECIMAL_FOR_FLOATS);

    @Test
    void farmResponsesMatchGoldenJson() throws IOException {
        FarmListItemResponse item = new FarmListItemResponse(
                FARM_ID, TENANT_ID, "FARM_A", "Farm A", "Long An",
                farmBoundaryResponse(), centerResponse(), new BigDecimal("12.5000"),
                FarmWireValues.ACTIVE, CREATED_AT, CREATED_BY);
        PageResponse<FarmListItemResponse> page = PageResponse.of(
                List.of(item), new PageRequest(1, 20), 1);
        FarmDetailResponse detail = new FarmDetailResponse(
                FARM_ID, TENANT_ID, "FARM_A", "Farm A", "Long An",
                farmBoundaryResponse(), centerResponse(), new BigDecimal("12.5000"),
                FarmWireValues.ACTIVE, CREATED_AT, CREATED_BY, UPDATED_AT, 3);
        ArchivedFarmResponse archived = new ArchivedFarmResponse(
                FARM_ID, TENANT_ID, "FARM_A", "Farm A", "Long An",
                farmBoundaryResponse(), centerResponse(), new BigDecimal("12.5000"),
                FarmWireValues.INACTIVE, CREATED_AT, CREATED_BY, UPDATED_AT,
                Instant.parse("2026-10-03T02:00:00Z"), 4);
        UpdateFarmResponse updated = new UpdateFarmResponse(
                FARM_ID, TENANT_ID, "FARM_A", "Farm A Updated", "Tien Giang",
                farmBoundaryResponse(), centerResponse(), new BigDecimal("13.2500"),
                FarmWireValues.ACTIVE, UPDATED_AT, 4);

        assertGolden("farm-page-response.json", page);
        assertGolden("farm-detail-response.json", detail);
        assertGolden("archived-farm-response.json", archived);
        assertGolden("update-farm-response.json", updated);
    }

    @Test
    void zoneResponsesMatchGoldenJson() throws IOException {
        ZoneListItemResponse item = new ZoneListItemResponse(
                ZONE_ID, FARM_ID, "ZONE_A", "Zone A", zoneBoundaryResponse(),
                new BigDecimal("2.5000"), FarmWireValues.ACTIVE, 2, CREATED_AT);
        ZoneDetailResponse detail = new ZoneDetailResponse(
                ZONE_ID, FARM_ID, "ZONE_A", "Zone A", zoneBoundaryResponse(),
                new BigDecimal("2.5000"), FarmWireValues.ACTIVE, 2, CREATED_AT,
                CREATED_BY, UPDATED_AT);
        CreateZoneResponse created = new CreateZoneResponse(
                ZONE_ID, FARM_ID, "ZONE_A", "Zone A", zoneBoundaryResponse(),
                new BigDecimal("2.5000"), FarmWireValues.ACTIVE, 1, CREATED_AT,
                CREATED_BY);
        UpdateZoneResponse updated = new UpdateZoneResponse(
                ZONE_ID, FARM_ID, "ZONE_A", "Zone A Updated", zoneBoundaryResponse(),
                new BigDecimal("2.7500"), FarmWireValues.ACTIVE, 3, UPDATED_AT);

        assertGolden("zone-list-response.json", List.of(item));
        assertGolden("zone-detail-response.json", detail);
        assertGolden("create-zone-response.json", created);
        assertGolden("update-zone-response.json", updated);
    }

    @Test
    void mutationRequestsMatchGoldenJson() throws IOException {
        UpdateFarmRequest updateFarm = new UpdateFarmRequest(
                "Farm A Updated", "Tien Giang", farmBoundaryRequest(), centerRequest(),
                new BigDecimal("13.2500"), 3);
        CreateZoneRequest createZone = new CreateZoneRequest(
                "ZONE_A", "Zone A", zoneBoundaryRequest(),
                new BigDecimal("2.5000"));
        UpdateZoneRequest updateZone = new UpdateZoneRequest(
                "Zone A Updated", zoneBoundaryRequest(),
                new BigDecimal("2.7500"), 2);

        assertGolden("update-farm-request.json", updateFarm);
        assertGolden("create-zone-request.json", createZone);
        assertGolden("update-zone-request.json", updateZone);
    }

    @Test
    void sharedErrorEnvelopeMatchesGoldenJson() throws IOException {
        ApiErrorResponse validation = new ApiErrorResponse(
                CREATED_AT,
                422,
                "Validation.Failed",
                "Request validation failed.",
                "40000000-0000-0000-0000-000000000001",
                "/api/farms/20000000-0000-0000-0000-000000000001",
                List.of(new ApiErrorResponse.Violation(
                        "name", "NotBlank", "Farm name is required.")));
        ApiErrorResponse notFound = new ApiErrorResponse(
                CREATED_AT,
                404,
                FarmErrorCodes.NOT_FOUND,
                "Farm was not found.",
                "40000000-0000-0000-0000-000000000001",
                "/api/farms/20000000-0000-0000-0000-000000000001",
                List.of());

        assertGolden("validation-error-response.json", validation);
        assertGolden("not-found-error-response.json", notFound);
    }

    @Test
    void optionalFieldsRemainPresentAndStatusRemainsNumeric() {
        FarmListItemResponse item = new FarmListItemResponse(
                FARM_ID, TENANT_ID, "FARM_A", "Farm A", null, null, null, null,
                FarmWireValues.ACTIVE, CREATED_AT, CREATED_BY);

        JsonNode json = JSON.valueToTree(item);

        assertThat(json.get("status").isIntegralNumber()).isTrue();
        assertThat(json.get("status").intValue()).isZero();
        assertThat(json.has("address")).isTrue();
        assertThat(json.get("address").isNull()).isTrue();
        assertThat(json.get("boundary").isNull()).isTrue();
        assertThat(json.get("centerPoint").isNull()).isTrue();
        assertThat(json.get("areaHectares").isNull()).isTrue();
    }

    @Test
    void farmAndZoneErrorCodesRemainCompatibleWithDotNet() {
        assertThat(List.of(
                FarmErrorCodes.NOT_FOUND,
                FarmErrorCodes.ACCESS_DENIED,
                FarmErrorCodes.CODE_ALREADY_EXISTS,
                FarmErrorCodes.CONCURRENT_UPDATE,
                FarmErrorCodes.ACTIVE_DEPENDENCIES_EXIST,
                FarmErrorCodes.INVALID_BOUNDARY,
                FarmErrorCodes.INVALID_CENTER_POINT,
                FarmErrorCodes.CENTER_POINT_OUTSIDE_BOUNDARY,
                FarmErrorCodes.INVALID_AREA,
                FarmErrorCodes.FARM_NOT_FOUND))
                .containsExactly(
                        "Farm.NotFound",
                        "Farm.AccessDenied",
                        "Farm.FarmCodeAlreadyExist",
                        "Farm.ConcurrentUpdate",
                        "Farm.ActiveDependenciesExist",
                        "Farm.InvalidBoundary",
                        "Farm.InvalidCenterPoint",
                        "Farm.CenterPointOutsideBoundary",
                        "Farm.InvalidArea",
                        "Farm.FarmNotFound");
        assertThat(List.of(
                FarmZoneErrorCodes.NOT_FOUND,
                FarmZoneErrorCodes.ACCESS_DENIED,
                FarmZoneErrorCodes.CODE_ALREADY_EXISTS,
                FarmZoneErrorCodes.CONCURRENT_UPDATE,
                FarmZoneErrorCodes.ACTIVE_DEPENDENCIES_EXIST,
                FarmZoneErrorCodes.INVALID_BOUNDARY,
                FarmZoneErrorCodes.INVALID_AREA,
                FarmZoneErrorCodes.BOUNDARY_OUTSIDE_FARM,
                FarmZoneErrorCodes.BOUNDARY_OVERLAPS))
                .containsExactly(
                        "FarmZone.NotFound",
                        "FarmZone.AccessDenied",
                        "FarmZone.CodeAlreadyExists",
                        "FarmZone.ConcurrentUpdate",
                        "FarmZone.ActiveDependenciesExist",
                        "FarmZone.InvalidBoundary",
                        "FarmZone.InvalidArea",
                        "FarmZone.BoundaryOutsideFarm",
                        "FarmZone.BoundaryOverlaps");
    }

    private static void assertGolden(String name, Object actual) throws IOException {
        JsonNode expected;
        try (var input = FarmContractSerializationTest.class.getResourceAsStream(
                "/contracts/farm/" + name)) {
            assertThat(input).as("golden fixture %s", name).isNotNull();
            expected = JSON.readTree(input);
        }
        JsonNode actualJson = JSON.readTree(JSON.writeValueAsBytes(actual));
        assertThat(actualJson).isEqualTo(expected);
    }

    private static GeoJsonPointRequest centerRequest() {
        return new GeoJsonPointRequest("Point", new double[]{106.1, 10.1});
    }

    private static GeoJsonPointResponse centerResponse() {
        return new GeoJsonPointResponse("Point", new double[]{106.1, 10.1});
    }

    private static GeoJsonPolygonRequest farmBoundaryRequest() {
        return new GeoJsonPolygonRequest("Polygon", new double[][][]{
            {{106.0, 10.0}, {106.2, 10.0}, {106.2, 10.2},
                    {106.0, 10.2}, {106.0, 10.0}}
        });
    }

    private static GeoJsonPolygonResponse farmBoundaryResponse() {
        return new GeoJsonPolygonResponse("Polygon", farmBoundaryRequest().coordinates());
    }

    private static GeoJsonPolygonRequest zoneBoundaryRequest() {
        return new GeoJsonPolygonRequest("Polygon", new double[][][]{
            {{106.0, 10.0}, {106.1, 10.0}, {106.1, 10.1},
                    {106.0, 10.1}, {106.0, 10.0}}
        });
    }

    private static GeoJsonPolygonResponse zoneBoundaryResponse() {
        return new GeoJsonPolygonResponse("Polygon", zoneBoundaryRequest().coordinates());
    }
}

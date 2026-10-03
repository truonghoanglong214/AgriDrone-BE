package com.agridrone.be1.farm.api.contract;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

public record ZoneListItemResponse(
        UUID zoneId,
        UUID farmId,
        String code,
        String name,
        GeoJsonPolygonResponse boundary,
        BigDecimal areaHectares,
        int status,
        long version,
        Instant createdAt) {}

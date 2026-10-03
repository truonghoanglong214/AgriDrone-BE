package com.agridrone.be1.farm.api.contract;

import com.agridrone.be1.farm.api.contract.validation.ValidGeoJsonPoint;

@ValidGeoJsonPoint
public record GeoJsonPointRequest(String type, double[] coordinates) {
    public GeoJsonPointRequest {
        coordinates = coordinates == null ? null : coordinates.clone();
    }

    @Override
    public double[] coordinates() {
        return coordinates == null ? null : coordinates.clone();
    }
}

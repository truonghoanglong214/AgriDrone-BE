package com.agridrone.be1.farm.api.contract;

public record GeoJsonPointResponse(String type, double[] coordinates) {
    public GeoJsonPointResponse {
        coordinates = coordinates == null ? null : coordinates.clone();
    }

    @Override
    public double[] coordinates() {
        return coordinates == null ? null : coordinates.clone();
    }
}

package com.agridrone.be1.farm.api.contract;

public record GeoJsonPolygonResponse(String type, double[][][] coordinates) {
    public GeoJsonPolygonResponse {
        coordinates = GeoJsonPolygonRequest.copy(coordinates);
    }

    @Override
    public double[][][] coordinates() {
        return GeoJsonPolygonRequest.copy(coordinates);
    }
}

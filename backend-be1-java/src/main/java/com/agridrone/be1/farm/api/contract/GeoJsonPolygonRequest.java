package com.agridrone.be1.farm.api.contract;

import com.agridrone.be1.farm.api.contract.validation.ValidGeoJsonPolygon;

@ValidGeoJsonPolygon
public record GeoJsonPolygonRequest(String type, double[][][] coordinates) {
    public GeoJsonPolygonRequest {
        coordinates = copy(coordinates);
    }

    @Override
    public double[][][] coordinates() {
        return copy(coordinates);
    }

    static double[][][] copy(double[][][] source) {
        if (source == null) {
            return null;
        }
        double[][][] result = new double[source.length][][];
        for (int ring = 0; ring < source.length; ring++) {
            if (source[ring] == null) {
                result[ring] = null;
                continue;
            }
            result[ring] = new double[source[ring].length][];
            for (int position = 0; position < source[ring].length; position++) {
                result[ring][position] = source[ring][position] == null
                        ? null
                        : source[ring][position].clone();
            }
        }
        return result;
    }
}

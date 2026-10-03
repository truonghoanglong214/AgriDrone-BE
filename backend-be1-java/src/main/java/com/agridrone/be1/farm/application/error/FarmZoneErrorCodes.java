package com.agridrone.be1.farm.application.error;

public final class FarmZoneErrorCodes {

    public static final String NOT_FOUND = "FarmZone.NotFound";
    public static final String ACCESS_DENIED = "FarmZone.AccessDenied";
    public static final String CODE_ALREADY_EXISTS = "FarmZone.CodeAlreadyExists";
    public static final String CONCURRENT_UPDATE = "FarmZone.ConcurrentUpdate";
    public static final String ACTIVE_DEPENDENCIES_EXIST = "FarmZone.ActiveDependenciesExist";
    public static final String INVALID_BOUNDARY = "FarmZone.InvalidBoundary";
    public static final String INVALID_AREA = "FarmZone.InvalidArea";
    public static final String BOUNDARY_OUTSIDE_FARM = "FarmZone.BoundaryOutsideFarm";
    public static final String BOUNDARY_OVERLAPS = "FarmZone.BoundaryOverlaps";

    private FarmZoneErrorCodes() {
    }
}

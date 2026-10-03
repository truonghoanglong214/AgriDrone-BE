package com.agridrone.be1.farm.application.error;

public final class FarmErrorCodes {

    public static final String NOT_FOUND = "Farm.NotFound";
    public static final String ACCESS_DENIED = "Farm.AccessDenied";
    public static final String CODE_ALREADY_EXISTS = "Farm.FarmCodeAlreadyExist";
    public static final String CONCURRENT_UPDATE = "Farm.ConcurrentUpdate";
    public static final String ACTIVE_DEPENDENCIES_EXIST = "Farm.ActiveDependenciesExist";
    public static final String INVALID_BOUNDARY = "Farm.InvalidBoundary";
    public static final String INVALID_CENTER_POINT = "Farm.InvalidCenterPoint";
    public static final String CENTER_POINT_OUTSIDE_BOUNDARY = "Farm.CenterPointOutsideBoundary";
    public static final String INVALID_AREA = "Farm.InvalidArea";
    public static final String FARM_NOT_FOUND = "Farm.FarmNotFound";

    private FarmErrorCodes() {
    }
}

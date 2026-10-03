package com.agridrone.be1.identity.application.error;

public final class SystemManagerErrorCodes {
    public static final String PROFILE_NOT_FOUND = "SystemManager.ProfileNotFound";
    public static final String PROFILE_ALREADY_EXISTS = "SystemManager.ProfileAlreadyExists";
    public static final String USER_MUST_BE_ACTIVE = "SystemManager.UserMustBeActive";
    public static final String ROLE_MISSING = "SystemManager.RoleMissing";
    public static final String NOT_ASSIGNABLE = "SystemManager.NotAssignable";
    public static final String INVALID_QUALIFICATION_EXPIRY =
            "SystemManager.InvalidQualificationExpiry";
    public static final String FARM_NOT_FOUND = "SystemManager.FarmNotFound";
    public static final String ASSIGNMENT_NOT_FOUND = "SystemManager.AssignmentNotFound";
    public static final String ACCESS_DENIED = "SystemManager.AccessDenied";
    public static final String CONCURRENT_UPDATE = "SystemManager.ConcurrentUpdate";
    public static final String ACTIVE_ASSIGNMENT_CONFLICT =
            "SystemManager.ActiveAssignmentConflict";

    private SystemManagerErrorCodes() {}
}

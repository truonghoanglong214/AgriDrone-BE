package com.agridrone.be1.identity.application.error;

public final class TenantErrorCodes {
    public static final String CODE_ALREADY_EXISTS = "Tenant.TenantCodeAlreadyExist";
    public static final String NOT_FOUND = "Tenant.NotFound";
    public static final String ACCESS_DENIED = "Tenant.AccessDenied";
    public static final String INACTIVE = "Tenant.Inactive";
    public static final String CONTEXT_REQUIRED = "Tenant.ContextRequired";

    private TenantErrorCodes() {
    }
}

package com.agridrone.be1.identity.application.error;

public final class AuthenticationErrorCodes {
    public static final String INVALID_CREDENTIALS = "Auth.InvalidCredentials";
    public static final String NO_TENANT_MEMBERSHIP = "Auth.NoTenantMembership";
    public static final String TENANT_SELECTION_REQUIRED = "Auth.TenantSelectionRequired";
    public static final String TOKEN_ISSUER_UNAVAILABLE = "Auth.TokenIssuerUnavailable";

    private AuthenticationErrorCodes() {
    }
}

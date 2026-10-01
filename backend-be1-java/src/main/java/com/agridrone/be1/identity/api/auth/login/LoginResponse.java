package com.agridrone.be1.identity.api.auth.login;

import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserResult;
import java.time.Instant;
import java.util.UUID;

public record LoginResponse(
        String email,
        String fullName,
        String phone,
        Session session) {

    public static LoginResponse from(LoginUserResult result) {
        LoginUserResult.Session source = result.session();
        LoginUserResult.Tenant sourceTenant = source.tenant();
        Tenant tenant = sourceTenant == null ? null : new Tenant(
                sourceTenant.id(), sourceTenant.code(), sourceTenant.name(), sourceTenant.role());
        return new LoginResponse(
                result.email(), result.fullName(), result.phone(),
                new Session(source.accessToken(), source.expiresAt(), tenant));
    }

    public record Session(String accessToken, Instant expiresAt, Tenant tenant) {
    }

    public record Tenant(UUID id, String code, String name, String role) {
    }
}

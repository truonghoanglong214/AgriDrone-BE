package com.agridrone.be1.identity.application.port.out.security;

import com.agridrone.be1.identity.application.security.IssuedTenantSelectionToken;
import java.util.Optional;
import java.util.UUID;

public interface TenantSelectionTokenService {
    IssuedTenantSelectionToken issue(UUID userId);

    Optional<UUID> validate(String token);
}

package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.SystemRole;
import java.util.Optional;

public interface RoleRepository {
    Optional<SystemRole> findByCode(String code);

    boolean existsActiveUserWithRole(String code);
}

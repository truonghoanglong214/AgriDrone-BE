package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.User;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;

public interface UserRepository {
    Optional<User> findById(UUID id);

    Optional<User> findByEmail(String email);

    Optional<User> findByEmailIncludingDeleted(String email);

    Set<String> findSystemRoleCodes(UUID userId);

    void save(User user);

    void assignSystemRole(UUID userId, UUID roleId);
}

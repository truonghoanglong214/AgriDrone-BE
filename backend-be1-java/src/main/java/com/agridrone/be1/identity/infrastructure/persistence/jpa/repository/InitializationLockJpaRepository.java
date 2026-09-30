package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.InitializationLockJpaEntity;
import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;

import java.util.Optional;

public interface InitializationLockJpaRepository
        extends JpaRepository<InitializationLockJpaEntity, String> {

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<InitializationLockJpaEntity> findByName(String name);
}

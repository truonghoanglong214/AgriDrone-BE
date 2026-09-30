package com.agridrone.be1.identity.infrastructure.initialization;

import com.agridrone.be1.identity.application.port.out.initialization.SystemAdminBootstrapLock;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.InitializationLockJpaRepository;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

@Component
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class JpaSystemAdminBootstrapLock implements SystemAdminBootstrapLock {
    private static final String LOCK_NAME = "system-admin-bootstrap";

    private final InitializationLockJpaRepository locks;

    public JpaSystemAdminBootstrapLock(
            InitializationLockJpaRepository locks) {
        this.locks = locks;
    }

    @Override
    @Transactional(propagation = Propagation.MANDATORY)
    public void acquire() {
        locks.findByName(LOCK_NAME)
                .orElseThrow(() -> new IllegalStateException(
                        "System admin bootstrap lock is missing"));
    }
}

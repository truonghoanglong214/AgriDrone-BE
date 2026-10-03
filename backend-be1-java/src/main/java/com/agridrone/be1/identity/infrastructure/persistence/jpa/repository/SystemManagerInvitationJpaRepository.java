package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.SystemManagerInvitationJpaEntity;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface SystemManagerInvitationJpaRepository
        extends JpaRepository<SystemManagerInvitationJpaEntity, UUID> {

    Optional<SystemManagerInvitationJpaEntity> findByTokenHash(String tokenHash);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select invitation from SystemManagerInvitationJpaEntity invitation "
            + "where invitation.tokenHash = :tokenHash")
    Optional<SystemManagerInvitationJpaEntity> findByTokenHashForUpdate(
            @Param("tokenHash") String tokenHash);

    Optional<SystemManagerInvitationJpaEntity>
            findFirstByEmailIgnoreCaseAndStatusOrderByCreatedAtDesc(
                    String email,
                    com.agridrone.be1.identity.domain.InvitationStatus status);

    @Override
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<SystemManagerInvitationJpaEntity> findById(UUID id);
}

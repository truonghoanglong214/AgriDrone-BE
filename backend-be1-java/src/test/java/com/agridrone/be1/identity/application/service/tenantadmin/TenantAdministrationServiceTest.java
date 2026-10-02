package com.agridrone.be1.identity.application.service.tenantadmin;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatExceptionOfType;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.port.in.tenantadmin.TenantAdministrationUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantStatus;
import com.agridrone.be1.shared.audit.AuditEntry;
import com.agridrone.be1.shared.audit.AuditWriter;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ActorType;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;
import com.agridrone.be1.shared.execution.ExecutionSource;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

class TenantAdministrationServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");
    private final TenantRepository tenants = org.mockito.Mockito.mock(TenantRepository.class);
    private final AuditWriter audit = org.mockito.Mockito.mock(AuditWriter.class);
    private final TenantAdministrationService service = new TenantAdministrationService(
            tenants, audit, Clock.fixed(NOW, ZoneOffset.UTC));

    @Test
    void createsActiveTenantWithNormalizedCodeAndAudit() {
        try (var ignored = ExecutionContext.begin(adminContext())) {
            var result = service.create(new TenantAdministrationUseCase.CreateTenantCommand(
                    " farm-a ", " Farm A "));

            assertThat(result.code()).isEqualTo("FARM-A");
            assertThat(result.name()).isEqualTo("Farm A");
            assertThat(result.status()).isEqualTo(TenantStatus.ACTIVE);
        }

        verify(tenants).existsByNormalizedCode("FARM-A");
        verify(tenants).save(org.mockito.ArgumentMatchers.argThat(
                tenant -> tenant.code().equals("FARM-A") && tenant.isActive()));
        ArgumentCaptor<AuditEntry> entry = ArgumentCaptor.forClass(AuditEntry.class);
        verify(audit).append(entry.capture());
        assertThat(entry.getValue().action()).isEqualTo("CREATE");
        assertThat(entry.getValue().after().path("code").asText()).isEqualTo("FARM-A");
    }

    @Test
    void duplicateCodeReturnsStableConflict() {
        when(tenants.existsByNormalizedCode("FARM-A")).thenReturn(true);

        try (var ignored = ExecutionContext.begin(adminContext())) {
            assertError(
                    () -> service.create(new TenantAdministrationUseCase.CreateTenantCommand(
                            "farm-a", "Farm A")),
                    TenantErrorCodes.CODE_ALREADY_EXISTS);
        }
        verify(tenants, never()).save(org.mockito.ArgumentMatchers.any());
    }

    @Test
    void activationAndDeactivationAreAuditedAndIdempotent() {
        UUID tenantId = UUID.randomUUID();
        Tenant inactive = new Tenant(
                tenantId, "FARM", "Farm", TenantStatus.INACTIVE,
                NOW.minusSeconds(60), NOW.minusSeconds(60), null);
        when(tenants.findByIdIncludingInactive(tenantId)).thenReturn(Optional.of(inactive));
        try (var ignored = ExecutionContext.begin(adminContext())) {
            service.activate(tenantId);
        }
        verify(tenants).save(org.mockito.ArgumentMatchers.argThat(Tenant::isActive));
        verify(audit).append(org.mockito.ArgumentMatchers.argThat(
                entry -> entry.action().equals("ACTIVATE")));

        org.mockito.Mockito.clearInvocations(tenants, audit);
        when(tenants.findByIdIncludingInactive(tenantId)).thenReturn(Optional.of(inactive));
        try (var ignored = ExecutionContext.begin(adminContext())) {
            service.activate(tenantId);
            service.deactivate(tenantId);
        }
        verify(tenants).save(org.mockito.ArgumentMatchers.argThat(
                tenant -> tenant.status() == TenantStatus.INACTIVE));
        verify(audit).append(org.mockito.ArgumentMatchers.argThat(
                entry -> entry.action().equals("DEACTIVATE")));
    }

    @Test
    void missingTenantReturnsStableNotFound() {
        UUID tenantId = UUID.randomUUID();
        when(tenants.findByIdIncludingInactive(tenantId)).thenReturn(Optional.empty());
        assertError(() -> service.deactivate(tenantId), TenantErrorCodes.NOT_FOUND);
    }

    private static ExecutionContextSnapshot adminContext() {
        return new ExecutionContextSnapshot(
                null,
                UUID.randomUUID(),
                ActorType.USER,
                UUID.randomUUID(),
                null,
                ExecutionSource.HTTP,
                Set.of("SYSTEM_ADMIN"));
    }

    private static void assertError(Runnable action, String code) {
        assertThatExceptionOfType(StableApiException.class)
                .isThrownBy(action::run)
                .satisfies(exception -> assertThat(exception.code()).isEqualTo(code));
    }
}

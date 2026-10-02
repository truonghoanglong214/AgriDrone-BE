package com.agridrone.be1.identity.application.service.tenantadmin;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.port.in.tenantadmin.TenantAdministrationUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.shared.audit.AuditEntry;
import com.agridrone.be1.shared.audit.AuditWriter;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.fasterxml.jackson.databind.node.JsonNodeFactory;
import java.time.Clock;
import java.time.Instant;
import java.util.Locale;
import java.util.UUID;
import org.springframework.http.HttpStatus;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class TenantAdministrationService implements TenantAdministrationUseCase {
    private final TenantRepository tenants;
    private final AuditWriter audit;
    private final Clock clock;

    public TenantAdministrationService(
            TenantRepository tenants,
            AuditWriter audit,
            Clock clock) {
        this.tenants = tenants;
        this.audit = audit;
        this.clock = clock;
    }

    @Override
    @Transactional
    public CreateTenantResult create(CreateTenantCommand command) {
        requireActor();
        String code = command.code().trim().toUpperCase(Locale.ROOT);
        String name = command.name().trim();
        if (tenants.existsByNormalizedCode(code)) {
            throw codeAlreadyExists(code);
        }

        Instant now = clock.instant();
        Tenant tenant = Tenant.create(code, name, now);
        tenants.save(tenant);
        audit.append(new AuditEntry(
                null,
                "Tenant",
                tenant.id(),
                "CREATE",
                null,
                tenantAuditData(tenant),
                null));
        return new CreateTenantResult(
                tenant.id(), tenant.code(), tenant.name(), tenant.status(), tenant.createdAt());
    }

    @Override
    @Transactional
    public void activate(UUID tenantId) {
        Tenant tenant = findTenant(tenantId);
        if (tenant.isActive()) {
            return;
        }
        requireActor();
        String previousStatus = tenant.status().name();
        tenant.activate(clock.instant());
        tenants.save(tenant);
        appendStatusAudit(tenant, "ACTIVATE", previousStatus);
    }

    @Override
    @Transactional
    public void deactivate(UUID tenantId) {
        Tenant tenant = findTenant(tenantId);
        if (!tenant.isActive()) {
            return;
        }
        requireActor();
        String previousStatus = tenant.status().name();
        tenant.deactivate(clock.instant());
        tenants.save(tenant);
        appendStatusAudit(tenant, "DEACTIVATE", previousStatus);
    }

    private Tenant findTenant(UUID tenantId) {
        return tenants.findByIdIncludingInactive(tenantId)
                .orElseThrow(() -> new StableApiException(
                        HttpStatus.NOT_FOUND,
                        TenantErrorCodes.NOT_FOUND,
                        "Tenant was not found."));
    }

    private void appendStatusAudit(Tenant tenant, String action, String previousStatus) {
        var before = JsonNodeFactory.instance.objectNode().put("status", previousStatus);
        var after = JsonNodeFactory.instance.objectNode().put("status", tenant.status().name());
        audit.append(new AuditEntry(
                null, "Tenant", tenant.id(), action, before, after, null));
    }

    private static com.fasterxml.jackson.databind.JsonNode tenantAuditData(Tenant tenant) {
        return JsonNodeFactory.instance.objectNode()
                .put("code", tenant.code())
                .put("name", tenant.name())
                .put("status", tenant.status().name());
    }

    private static void requireActor() {
        if (ExecutionContext.require().actorId() == null) {
            throw new StableApiException(
                    HttpStatus.UNAUTHORIZED,
                    AuthenticationErrorCodes.CURRENT_USER_REQUIRED,
                    "A current user is required.");
        }
    }

    private static StableApiException codeAlreadyExists(String code) {
        return new StableApiException(
                HttpStatus.CONFLICT,
                TenantErrorCodes.CODE_ALREADY_EXISTS,
                "Tenant with Tenant Code '" + code + "' already exists.");
    }
}

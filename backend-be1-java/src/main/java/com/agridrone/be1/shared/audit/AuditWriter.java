package com.agridrone.be1.shared.audit;

import com.agridrone.be1.shared.execution.ExecutionContext;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;
import org.springframework.transaction.support.TransactionSynchronizationManager;

@Component
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class AuditWriter {
    private final AuditRepository repository;
    private final ObjectMapper objectMapper;

    public AuditWriter(AuditRepository repository, ObjectMapper objectMapper) {
        this.repository = repository;
        this.objectMapper = objectMapper;
    }

    public void append(AuditEntry entry) {
        if (!TransactionSynchronizationManager.isActualTransactionActive()) {
            throw new IllegalStateException("Audit entries must be written inside the business transaction");
        }
        repository.append(ExecutionContext.require(), entry, json(entry.before()), json(entry.after()));
    }

    private String json(Object value) {
        if (value == null) {
            return null;
        }
        try {
            return objectMapper.writeValueAsString(value);
        } catch (JsonProcessingException exception) {
            throw new IllegalArgumentException("Audit payload is not valid JSON", exception);
        }
    }
}

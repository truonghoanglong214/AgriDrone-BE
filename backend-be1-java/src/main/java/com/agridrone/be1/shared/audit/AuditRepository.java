package com.agridrone.be1.shared.audit;

import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;

public interface AuditRepository {
    void append(ExecutionContextSnapshot context, AuditEntry entry, String beforeJson, String afterJson);
}

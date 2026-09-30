package com.agridrone.be1.identity.domain;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.util.UUID;
import org.junit.jupiter.api.Test;

class SystemRoleTest {

    @Test
    void acceptsSupportedRoleCode() {
        UUID id = UUID.randomUUID();

        SystemRole role = new SystemRole(id, SystemRoleCodes.SYSTEM_ADMIN);

        assertThat(role.id()).isEqualTo(id);
        assertThat(role.code()).isEqualTo(SystemRoleCodes.SYSTEM_ADMIN);
    }

    @Test
    void rejectsUnsupportedRoleCode() {
        assertThatThrownBy(() -> new SystemRole(UUID.randomUUID(), "TENANT_ADMIN"))
                .isInstanceOf(IllegalArgumentException.class)
                .hasMessage("Unsupported system role code: TENANT_ADMIN");
    }

    @Test
    void rejectsMissingId() {
        assertThatThrownBy(() -> new SystemRole(null, SystemRoleCodes.SYSTEM_MANAGER))
                .isInstanceOf(NullPointerException.class)
                .hasMessage("id");
    }
}

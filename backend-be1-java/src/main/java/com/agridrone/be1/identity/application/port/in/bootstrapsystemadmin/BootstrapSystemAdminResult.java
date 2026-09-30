package com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin;

import java.util.UUID;

public record BootstrapSystemAdminResult(
        boolean created,
        UUID userId,
        String email) {
}
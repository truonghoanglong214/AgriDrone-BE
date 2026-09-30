package com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin;

public record BootstrapSystemAdminCommand(
        String email,
        String fullName) {
}

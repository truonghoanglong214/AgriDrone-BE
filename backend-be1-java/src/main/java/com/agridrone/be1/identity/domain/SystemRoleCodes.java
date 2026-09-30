package com.agridrone.be1.identity.domain;

import java.util.Set;

public final class SystemRoleCodes {
    public static final String SYSTEM_ADMIN = "SYSTEM_ADMIN";
    public static final String SYSTEM_MANAGER = "SYSTEM_MANAGER";

    public static final Set<String> ALL = Set.of(
            SYSTEM_ADMIN,
            SYSTEM_MANAGER);

    private SystemRoleCodes() {
    }

    public static boolean contains(String code) {
        return code != null && ALL.contains(code);
    }
}

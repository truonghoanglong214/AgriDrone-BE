package com.agridrone.be1.shared.auth;

import java.util.UUID;

@FunctionalInterface
public interface TokenIdGenerator {
    UUID next();
}

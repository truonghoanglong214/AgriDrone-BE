package com.agridrone.be1.identity.api.systemmanager;

import jakarta.validation.constraints.NotNull;
import java.util.UUID;

public record CreateSystemManagerProfileRequest(@NotNull UUID userId) {}

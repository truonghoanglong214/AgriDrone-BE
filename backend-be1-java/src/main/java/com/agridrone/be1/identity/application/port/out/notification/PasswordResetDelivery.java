package com.agridrone.be1.identity.application.port.out.notification;

import java.time.Instant;

public interface PasswordResetDelivery {
    void send(String email, String fullName, String plainTextToken, Instant expiresAt);
}

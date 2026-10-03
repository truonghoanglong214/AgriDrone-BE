package com.agridrone.be1.identity.application.port.out.notification;

import java.time.Instant;

public interface SystemManagerInvitationDelivery {
    void send(String email, String plainTextToken, Instant expiresAt);
}

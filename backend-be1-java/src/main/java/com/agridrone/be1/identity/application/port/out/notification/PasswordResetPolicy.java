package com.agridrone.be1.identity.application.port.out.notification;

import java.time.Duration;

public interface PasswordResetPolicy {
    Duration expiration();
}

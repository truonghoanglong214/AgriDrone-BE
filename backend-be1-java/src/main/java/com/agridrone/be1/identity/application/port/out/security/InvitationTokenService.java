package com.agridrone.be1.identity.application.port.out.security;

import com.agridrone.be1.identity.application.security.GeneratedInvitationToken;

public interface InvitationTokenService {
    GeneratedInvitationToken generate();

    String hash(String plainTextToken);
}

package com.agridrone.be1.identity.application.port.in.resetpassword;

public interface ResetPasswordUseCase {
    ResetPasswordResult reset(ResetPasswordCommand command);
}

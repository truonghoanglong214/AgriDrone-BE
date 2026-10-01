package com.agridrone.be1.identity.application.port.in.forgotpassword;

public interface ForgotPasswordUseCase {
    ForgotPasswordResult requestReset(ForgotPasswordCommand command);
}

package com.agridrone.be1.identity.application.port.in.loginuser;

public interface LoginUserUseCase {
    LoginUserResult login(LoginUserCommand command);
}

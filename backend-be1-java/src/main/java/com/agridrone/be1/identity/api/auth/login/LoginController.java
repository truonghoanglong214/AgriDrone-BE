package com.agridrone.be1.identity.api.auth.login;

import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserCommand;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserUseCase;
import com.agridrone.be1.shared.api.OpenApiTags;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/auth")
@Tag(name = OpenApiTags.AUTHENTICATION,
        description = OpenApiTags.AUTHENTICATION_DESCRIPTION)
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public class LoginController {
    private final LoginUserUseCase login;

    public LoginController(LoginUserUseCase login) {
        this.login = login;
    }

    @PostMapping("/login")
    public ResponseEntity<LoginResponse> login(@Valid @RequestBody LoginRequest request) {
        return ResponseEntity.ok().body(LoginResponse.from(
                login.login(new LoginUserCommand(request.email(), request.password()))));
    }
}

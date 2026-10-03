package com.agridrone.be1.identity.api.auth.passwordreset;

import com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordCommand;
import com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordUseCase;
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
        prefix = "agridrone.notification.smtp",
        name = "enabled",
        havingValue = "true")
public class ForgotPasswordController {
    private final ForgotPasswordUseCase forgotPassword;

    public ForgotPasswordController(ForgotPasswordUseCase forgotPassword) {
        this.forgotPassword = forgotPassword;
    }

    @PostMapping("/forgot-password")
    public ResponseEntity<ForgotPasswordResponse> forgotPassword(
            @Valid @RequestBody ForgotPasswordRequest request) {
        var result = forgotPassword.requestReset(new ForgotPasswordCommand(request.email()));
        return ResponseEntity.accepted().body(new ForgotPasswordResponse(result.message()));
    }
}

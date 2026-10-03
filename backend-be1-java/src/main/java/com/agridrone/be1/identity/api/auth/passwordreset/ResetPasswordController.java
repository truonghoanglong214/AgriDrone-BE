package com.agridrone.be1.identity.api.auth.passwordreset;

import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordCommand;
import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordUseCase;
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
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class ResetPasswordController {
    private final ResetPasswordUseCase resetPassword;

    public ResetPasswordController(ResetPasswordUseCase resetPassword) {
        this.resetPassword = resetPassword;
    }

    @PostMapping("/reset-password")
    public ResponseEntity<ResetPasswordResponse> resetPassword(
            @Valid @RequestBody ResetPasswordRequest request) {
        var result = resetPassword.reset(new ResetPasswordCommand(
                request.token(), request.newPassword(), request.confirmPassword()));
        return ResponseEntity.ok(new ResetPasswordResponse(result.message()));
    }
}

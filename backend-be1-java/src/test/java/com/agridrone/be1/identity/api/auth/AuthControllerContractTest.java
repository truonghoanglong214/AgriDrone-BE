package com.agridrone.be1.identity.api.auth;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.agridrone.be1.identity.api.auth.login.LoginController;
import com.agridrone.be1.identity.api.auth.passwordreset.ForgotPasswordController;
import com.agridrone.be1.identity.api.auth.passwordreset.ResetPasswordController;
import com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordResult;
import com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordUseCase;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserResult;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserUseCase;
import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordResult;
import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordUseCase;
import com.agridrone.be1.shared.error.GlobalApiExceptionHandler;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.http.converter.json.MappingJackson2HttpMessageConverter;

class AuthControllerContractTest {

    private MockMvc mockMvc;
    private LoginUserUseCase login;
    private ForgotPasswordUseCase forgotPassword;
    private ResetPasswordUseCase resetPassword;

    @BeforeEach
    void setUp() {
        login = mock(LoginUserUseCase.class);
        forgotPassword = mock(ForgotPasswordUseCase.class);
        resetPassword = mock(ResetPasswordUseCase.class);
        ObjectMapper objectMapper = new ObjectMapper()
                .findAndRegisterModules()
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        mockMvc = MockMvcBuilders
                .standaloneSetup(
                        new LoginController(login),
                        new ForgotPasswordController(forgotPassword),
                        new ResetPasswordController(resetPassword))
                .setMessageConverters(new MappingJackson2HttpMessageConverter(objectMapper))
                .setControllerAdvice(new GlobalApiExceptionHandler(
                        Clock.fixed(Instant.parse("2026-09-28T00:00:00Z"), ZoneOffset.UTC)))
                .build();
    }

    @Test
    void loginReturnsStableSessionShape() throws Exception {
        when(login.login(any())).thenReturn(new LoginUserResult(
                "admin@example.com", "System Admin", null,
                new LoginUserResult.Session(
                        "signed-token", Instant.parse("2026-09-28T00:15:00Z"), null),
                null));

        mockMvc.perform(post("/api/auth/login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"email\":\"admin@example.com\",\"password\":\"correct-password\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.email").value("admin@example.com"))
                .andExpect(jsonPath("$.session.accessToken").value("signed-token"))
                .andExpect(jsonPath("$.session.tenant").doesNotExist());
    }

    @Test
    void multiTenantLoginReturnsSelectionPayloadWithoutAccessToken() throws Exception {
        when(login.login(any())).thenReturn(new LoginUserResult(
                "owner@example.com", "Tenant Owner", null,
                null,
                new LoginUserResult.TenantSelection(
                        "selection-token",
                        Instant.parse("2026-09-28T00:05:00Z"),
                        List.of(
                                new LoginUserResult.Tenant(
                                        UUID.fromString("11111111-1111-1111-1111-111111111111"),
                                        "ALPHA",
                                        "Alpha Farm",
                                        "OWNER"),
                                new LoginUserResult.Tenant(
                                        UUID.fromString("22222222-2222-2222-2222-222222222222"),
                                        "ZULU",
                                        "Zulu Farm",
                                        "OWNER")))));

        mockMvc.perform(post("/api/auth/login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"email\":\"owner@example.com\",\"password\":\"correct-password\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.session").doesNotExist())
                .andExpect(jsonPath("$.tenantSelection.selectionToken").value("selection-token"))
                .andExpect(jsonPath("$.tenantSelection.expiresAt")
                        .value("2026-09-28T00:05:00Z"))
                .andExpect(jsonPath("$.tenantSelection.tenants[0].name").value("Alpha Farm"))
                .andExpect(jsonPath("$.tenantSelection.tenants[0].role").value(0))
                .andExpect(jsonPath("$..accessToken").doesNotExist());
    }

    @Test
    void forgotPasswordAlwaysReturnsAcceptedGenericMessage() throws Exception {
        when(forgotPassword.requestReset(any()))
                .thenReturn(new ForgotPasswordResult("If an account exists for this email, a password reset link has been sent."));

        mockMvc.perform(post("/api/auth/forgot-password")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"email\":\"unknown@example.com\"}"))
                .andExpect(status().isAccepted())
                .andExpect(jsonPath("$.message").value(
                        "If an account exists for this email, a password reset link has been sent."));
    }

    @Test
    void resetPasswordReturnsSuccessMessage() throws Exception {
        when(resetPassword.reset(any()))
                .thenReturn(new ResetPasswordResult("Password has been reset successfully."));

        mockMvc.perform(post("/api/auth/reset-password")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"token\":\"token\",\"newPassword\":\"new-password\",\"confirmPassword\":\"new-password\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.message").value("Password has been reset successfully."));
    }

    @Test
    void invalidLoginPayloadUsesValidationStatus() throws Exception {
        mockMvc.perform(post("/api/auth/login")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"email\":\"not-an-email\",\"password\":\"\"}"))
                .andExpect(status().isUnprocessableEntity())
                .andExpect(jsonPath("$.code").value("Validation.Failed"));
    }
}

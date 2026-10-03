package com.agridrone.be1.identity.api;

import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.agridrone.be1.identity.api.systemmanager.SystemFarmManagersController;
import com.agridrone.be1.identity.api.systemmanager.SystemManagerWorkController;
import com.agridrone.be1.identity.api.systemmanager.SystemManagersController;
import com.agridrone.be1.identity.api.systemmanagerinvitation.SystemManagerInvitationController;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerInvitationUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerWorkUseCase;
import com.agridrone.be1.shared.auth.SecurityConfiguration;
import com.agridrone.be1.shared.auth.SecurityErrorWriter;
import com.agridrone.be1.shared.execution.PlatformConfiguration;
import com.agridrone.be1.shared.execution.WebPlatformProperties;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(
        controllers = {
            SystemManagersController.class,
            SystemFarmManagersController.class,
            SystemManagerWorkController.class,
            SystemManagerInvitationController.class
        },
        properties = {
            "agridrone.runtime.enabled=true",
            "agridrone.security.jwt.enabled=true",
            "agridrone.security.jwt.jwk-set-uri=https://jwt.test/.well-known/jwks.json",
            "agridrone.security.jwt.issuer=test-issuer",
            "agridrone.security.jwt.audience=test-audience"
        })
@Import({SecurityConfiguration.class, SecurityErrorWriter.class, PlatformConfiguration.class})
@EnableConfigurationProperties(WebPlatformProperties.class)
class Phase4dAuthorizationTest {
    private static final UUID USER_ID = UUID.randomUUID();

    @Autowired
    MockMvc mockMvc;

    @MockBean
    JwtDecoder jwtDecoder;

    @MockBean
    SystemManagerAdministrationUseCase administration;

    @MockBean
    SystemManagerInvitationUseCase invitations;

    @MockBean
    SystemManagerWorkUseCase work;

    @BeforeEach
    void configureTokens() {
        when(jwtDecoder.decode(anyString())).thenAnswer(invocation ->
                jwt(invocation.getArgument(0), List.of("MEMBER")));
    }

    @Test
    void nonAdminCannotManageManagers() throws Exception {
        mockMvc.perform(post("/api/system/managers")
                        .header("Authorization", "Bearer member-token")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + USER_ID + "\"}"))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("Auth.AccessDenied"));
    }

    @Test
    void nonManagerCannotReadAssignedFarms() throws Exception {
        mockMvc.perform(get("/api/system-manager/farms")
                        .header("Authorization", "Bearer member-token"))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("Auth.AccessDenied"));
    }

    @Test
    void invitationPreviewIsAnonymous() throws Exception {
        when(invitations.preview("plain-token")).thenReturn(
                new SystemManagerInvitationUseCase.PreviewResult(
                        "m***@example.com",
                        "SYSTEM_MANAGER",
                        Instant.now().plusSeconds(300),
                        true));

        mockMvc.perform(post("/api/auth/system-manager-invitations/preview")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"token\":\"plain-token\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.role").value("SYSTEM_MANAGER"));
    }

    private static Jwt jwt(String token, List<String> roles) {
        Instant now = Instant.now();
        return Jwt.withTokenValue(token)
                .header("alg", "RS256")
                .subject(USER_ID.toString())
                .issuedAt(now.minusSeconds(10))
                .expiresAt(now.plusSeconds(300))
                .claim("system_role", roles)
                .build();
    }
}

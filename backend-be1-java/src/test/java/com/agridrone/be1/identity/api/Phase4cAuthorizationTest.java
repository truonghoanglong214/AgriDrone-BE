package com.agridrone.be1.identity.api;

import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.agridrone.be1.identity.api.tenant.SystemTenantsController;
import com.agridrone.be1.identity.api.tenant.SystemUserTenantsController;
import com.agridrone.be1.identity.application.port.in.tenantadmin.TenantAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantinvitation.TenantInvitationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantquery.TenantQueryUseCase;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
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
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(
        controllers = {SystemTenantsController.class, SystemUserTenantsController.class},
        properties = {
            "agridrone.runtime.enabled=true",
            "agridrone.security.jwt.enabled=true",
            "agridrone.security.jwt.jwk-set-uri=https://jwt.test/.well-known/jwks.json",
            "agridrone.security.jwt.issuer=test-issuer",
            "agridrone.security.jwt.audience=test-audience"
        })
@Import({SecurityConfiguration.class, SecurityErrorWriter.class, PlatformConfiguration.class})
@EnableConfigurationProperties(WebPlatformProperties.class)
class Phase4cAuthorizationTest {
    private static final UUID USER_ID = UUID.randomUUID();

    @Autowired
    MockMvc mockMvc;

    @MockBean
    JwtDecoder jwtDecoder;

    @MockBean
    TenantAdministrationUseCase administration;

    @MockBean
    TenantInvitationUseCase invitations;

    @MockBean
    TenantQueryUseCase queries;

    @BeforeEach
    void configureTokens() {
        when(jwtDecoder.decode(anyString())).thenAnswer(invocation ->
                jwt(invocation.getArgument(0), List.of("MEMBER")));
    }

    @Test
    void nonSystemAdminCannotUseTenantLifecycleOrReadModels() throws Exception {
        mockMvc.perform(post("/api/system/tenants")
                        .header("Authorization", "Bearer member-token")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"tenantCode\":\"FARM\",\"tenantName\":\"Farm\"}"))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("Auth.AccessDenied"));

        mockMvc.perform(get("/api/system/tenants/all")
                        .header("Authorization", "Bearer member-token"))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("Auth.AccessDenied"));

        mockMvc.perform(get("/api/system/users/{userId}/tenants", USER_ID)
                        .header("Authorization", "Bearer member-token"))
                .andExpect(status().isForbidden())
                .andExpect(jsonPath("$.code").value("Auth.AccessDenied"));
    }

    @Test
    void systemAdminCanReachReadModel() throws Exception {
        when(jwtDecoder.decode("admin-token"))
                .thenReturn(jwt("admin-token", List.of("SYSTEM_ADMIN")));
        when(queries.findTenants(PageRequest.defaults()))
                .thenReturn(PageResponse.of(List.of(), PageRequest.defaults(), 0));

        mockMvc.perform(get("/api/system/tenants/all")
                        .header("Authorization", "Bearer admin-token"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.items").isEmpty());
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

package com.agridrone.be1;

import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.agridrone.be1.shared.execution.ExecutionContext;
import java.time.Instant;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.context.annotation.Import;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@SpringBootTest(properties = {
        "agridrone.security.jwt.enabled=true",
        "agridrone.security.jwt.jwk-set-uri=https://jwt.test/.well-known/jwks.json",
        "agridrone.security.jwt.issuer=test-issuer",
        "agridrone.security.jwt.audience=test-audience"
})
@AutoConfigureMockMvc
@ActiveProfiles("test")
@Import(SecurityExecutionContextIT.ProbeController.class)
class SecurityExecutionContextIT {
    private static final UUID ACTOR_ID = UUID.fromString("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static final UUID TENANT_ID = UUID.fromString("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private JwtDecoder jwtDecoder;

    @BeforeEach
    void configureDecoder() {
        Instant now = Instant.now().minusSeconds(30);
        Jwt jwt = Jwt.withTokenValue("test-token")
                .header("alg", "RS256")
                .header("kid", "test-key")
                .issuer("test-issuer")
                .audience(List.of("test-audience"))
                .subject(ACTOR_ID.toString())
                .issuedAt(now.minusSeconds(30))
                .notBefore(now.minusSeconds(30))
                .expiresAt(now.plusSeconds(300))
                .claim("tenant_id", TENANT_ID.toString())
                .claim("system_role", List.of("SYSTEM_ADMIN"))
                .build();
        when(jwtDecoder.decode("test-token")).thenReturn(jwt);
    }

    @Test
    void authenticatedJwtPopulatesActorTenantAndRoleAfterBearerAuthentication() throws Exception {
        mockMvc.perform(get("/test/execution-context")
                        .header("Authorization", "Bearer test-token"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.actorType").value("USER"))
                .andExpect(jsonPath("$.actorId").value(ACTOR_ID.toString()))
                .andExpect(jsonPath("$.tenantId").value(TENANT_ID.toString()))
                .andExpect(jsonPath("$.roles[0]").value("SYSTEM_ADMIN"));
    }

    @RestController
    static class ProbeController {
        @GetMapping("/test/execution-context")
        ContextView context() {
            var context = ExecutionContext.require();
            return new ContextView(
                    context.actorType().name(),
                    context.actorId().toString(),
                    context.tenantId().toString(),
                    context.roles());
        }
    }

    record ContextView(String actorType, String actorId, String tenantId, Set<String> roles) {
    }
}

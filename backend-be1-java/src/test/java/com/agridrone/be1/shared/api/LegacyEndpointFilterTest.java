package com.agridrone.be1.shared.api;

import static org.assertj.core.api.Assertions.assertThat;

import com.agridrone.be1.shared.auth.SecurityErrorWriter;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.concurrent.atomic.AtomicBoolean;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;

class LegacyEndpointFilterTest {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");

    @ParameterizedTest
    @CsvSource({
        "POST,/api/auth/register",
        "POST,/api/farms",
        "PUT,/api/farms/0d7291ef-cff2-49e7-94d4-73681a743b2c/restore",
        "POST,/api/tenants/current/transfer-ownership"
    })
    void retiredMutationReturnsStableGoneWithoutCallingApplicationHandler(
            String method,
            String path) throws Exception {
        ObjectMapper mapper = new ObjectMapper().registerModule(new JavaTimeModule());
        var filter = new LegacyEndpointFilter(new SecurityErrorWriter(
                mapper,
                Clock.fixed(NOW, ZoneOffset.UTC)));
        var request = new MockHttpServletRequest(method, path);
        var response = new MockHttpServletResponse();
        var continued = new AtomicBoolean();

        filter.doFilter(request, response, (ignoredRequest, ignoredResponse) -> continued.set(true));

        assertThat(response.getStatus()).isEqualTo(410);
        assertThat(response.getContentType()).isEqualTo("application/json");
        assertThat(mapper.readTree(response.getContentAsByteArray()).path("code").asText())
                .isEqualTo("LegacyFlow.Disabled");
        assertThat(continued).isFalse();
    }
}

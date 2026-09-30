package com.agridrone.be1.shared.execution;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockFilterChain;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;

class CorrelationIdFilterTest {

    private final CorrelationIdFilter filter =
            new CorrelationIdFilter(new WebPlatformProperties("X-Correlation-ID"));

    @Test
    void preservesValidCorrelationIdentifier() throws Exception {
        String expected = UUID.randomUUID().toString();
        MockHttpServletRequest request = new MockHttpServletRequest();
        request.addHeader("X-Correlation-ID", expected);
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, new MockFilterChain());

        assertThat(response.getHeader("X-Correlation-ID")).isEqualTo(expected);
    }

    @Test
    void replacesInvalidUntrustedCorrelationIdentifier() throws Exception {
        MockHttpServletRequest request = new MockHttpServletRequest();
        request.addHeader("X-Correlation-ID", "not-a-uuid");
        MockHttpServletResponse response = new MockHttpServletResponse();

        filter.doFilter(request, response, new MockFilterChain());

        assertThat(UUID.fromString(response.getHeader("X-Correlation-ID"))).isNotNull();
    }
}

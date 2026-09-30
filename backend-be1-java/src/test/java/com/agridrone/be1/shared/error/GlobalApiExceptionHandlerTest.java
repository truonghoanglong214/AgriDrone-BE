package com.agridrone.be1.shared.error;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.agridrone.be1.shared.execution.CorrelationIdFilter;
import com.agridrone.be1.shared.execution.WebPlatformProperties;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.http.MediaType;
import org.springframework.http.converter.json.MappingJackson2HttpMessageConverter;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

class GlobalApiExceptionHandlerTest {

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        Clock clock = Clock.fixed(Instant.parse("2026-09-28T00:00:00Z"), ZoneOffset.UTC);
        ObjectMapper objectMapper = new ObjectMapper()
                .registerModule(new JavaTimeModule())
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        mockMvc = MockMvcBuilders
                .standaloneSetup(new ValidationProbeController())
                .setControllerAdvice(new GlobalApiExceptionHandler(clock))
                .setMessageConverters(new MappingJackson2HttpMessageConverter(objectMapper))
                .addFilters(new CorrelationIdFilter(
                        new WebPlatformProperties("X-Correlation-ID")))
                .build();
    }

    @Test
    void validationUsesStableEnvelopeWithoutRejectedValue() throws Exception {
        mockMvc.perform(post("/__phase1/validation-probe")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{}"))
                .andExpect(status().isUnprocessableEntity())
                .andExpect(header().exists("X-Correlation-ID"))
                .andExpect(jsonPath("$.timestamp").value("2026-09-28T00:00:00Z"))
                .andExpect(jsonPath("$.status").value(422))
                .andExpect(jsonPath("$.code").value("Validation.Failed"))
                .andExpect(jsonPath("$.violations[0].field").value("name"))
                .andExpect(jsonPath("$.violations[0].rejectedValue").doesNotExist());
    }

    @RestController
    static class ValidationProbeController {

        @PostMapping("/__phase1/validation-probe")
        void validate(@Valid @RequestBody ProbeRequest request) {
        }
    }

    record ProbeRequest(@NotBlank String name) {
    }
}

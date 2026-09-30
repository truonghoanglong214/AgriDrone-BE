package com.agridrone.be1.shared.auth;

import com.agridrone.be1.shared.error.ApiErrorResponse;
import com.agridrone.be1.shared.execution.CorrelationIds;
import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;

@Component
public class SecurityErrorWriter {

    private final ObjectMapper objectMapper;
    private final Clock clock;

    public SecurityErrorWriter(ObjectMapper objectMapper, Clock clock) {
        this.objectMapper = objectMapper;
        this.clock = clock;
    }

    public void write(
            HttpServletRequest request,
            HttpServletResponse response,
            int status,
            String code,
            String message) throws IOException {
        response.setStatus(status);
        response.setContentType(MediaType.APPLICATION_JSON_VALUE);
        objectMapper.writeValue(
                response.getOutputStream(),
                new ApiErrorResponse(
                        Instant.now(clock),
                        status,
                        code,
                        message,
                        CorrelationIds.currentOrCreate(),
                        request.getRequestURI(),
                        List.of()));
    }
}

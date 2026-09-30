package com.agridrone.be1.shared.messaging;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.core.JsonGenerator;
import com.fasterxml.jackson.databind.JsonSerializer;
import com.fasterxml.jackson.databind.SerializerProvider;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.databind.module.SimpleModule;
import java.io.IOException;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeFormatterBuilder;
import org.springframework.stereotype.Component;

@Component
public class IntegrationEventSerializer {
    public static final int MAX_BYTES = 4 * 1024 * 1024;
    private final ObjectMapper objectMapper;

    public IntegrationEventSerializer(ObjectMapper objectMapper) {
        DateTimeFormatter crossLanguageOffset = new DateTimeFormatterBuilder()
                .append(DateTimeFormatter.ISO_LOCAL_DATE_TIME)
                .appendOffset("+HH:MM", "+00:00")
                .toFormatter();
        SimpleModule module = new SimpleModule().addSerializer(OffsetDateTime.class, new JsonSerializer<>() {
            @Override
            public void serialize(OffsetDateTime value, JsonGenerator generator, SerializerProvider serializers)
                    throws IOException {
                generator.writeString(crossLanguageOffset.format(value));
            }
        });
        this.objectMapper = objectMapper.copy().registerModule(module)
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
                .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES);
    }

    public byte[] serialize(IntegrationEventEnvelope envelope) {
        IntegrationEventRegistry.validate(envelope.eventType(), envelope.schemaVersion());
        try {
            byte[] bytes = objectMapper.writeValueAsBytes(envelope);
            validateSize(bytes);
            return bytes;
        } catch (JsonProcessingException exception) {
            throw new IllegalArgumentException("Cannot serialize integration event", exception);
        }
    }

    public IntegrationEventEnvelope deserialize(byte[] bytes) {
        validateSize(bytes);
        try {
            IntegrationEventEnvelope envelope = objectMapper.readValue(bytes, IntegrationEventEnvelope.class);
            IntegrationEventRegistry.validate(envelope.eventType(), envelope.schemaVersion());
            return envelope;
        } catch (IOException exception) {
            throw new IllegalArgumentException("Invalid integration-event JSON", exception);
        }
    }

    private static void validateSize(byte[] bytes) {
        if (bytes == null || bytes.length == 0 || bytes.length > MAX_BYTES) {
            throw new IllegalArgumentException("Integration-event body must be between 1 byte and 4 MiB");
        }
    }
}

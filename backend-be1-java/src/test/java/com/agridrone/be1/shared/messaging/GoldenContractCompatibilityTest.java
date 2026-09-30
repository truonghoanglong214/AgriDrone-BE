package com.agridrone.be1.shared.messaging;

import static org.assertj.core.api.Assertions.assertThat;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashSet;
import java.util.Set;
import org.junit.jupiter.api.Test;

class GoldenContractCompatibilityTest {
    private final ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
    private final IntegrationEventSerializer serializer = new IntegrationEventSerializer(mapper);

    @Test
    void javaReadsAndWritesEveryCrossServiceGoldenFixtureWithoutTypeMetadata() throws Exception {
        Path fixtures = Path.of("..", "contracts", "examples", "events");
        Set<String> seen = new HashSet<>();
        try (var paths = Files.list(fixtures)) {
            for (Path path : paths.filter(value -> value.getFileName().toString().endsWith(".event.json")).toList()) {
                byte[] source = Files.readAllBytes(path);
                IntegrationEventEnvelope envelope = serializer.deserialize(source);
                byte[] roundTrip = serializer.serialize(envelope);
                JsonNode original = mapper.readTree(source);
                JsonNode actual = mapper.readTree(roundTrip);
                assertThat(actual).as(path.toString()).isEqualTo(original);
                assertThat(new String(roundTrip)).doesNotContain("$type", "System.", "PublicKeyToken");
                seen.add(envelope.eventType());
            }
        }
        assertThat(seen).isEqualTo(IntegrationEventRegistry.contracts().keySet());
    }
}

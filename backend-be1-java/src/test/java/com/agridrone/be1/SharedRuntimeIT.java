package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.agridrone.be1.shared.audit.AuditEntry;
import com.agridrone.be1.shared.audit.AuditWriter;
import com.agridrone.be1.shared.audit.JdbcAuditRepository;
import com.agridrone.be1.shared.execution.ActorType;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;
import com.agridrone.be1.shared.execution.ExecutionSource;
import com.agridrone.be1.shared.messaging.ConfirmedEventPublisher;
import com.agridrone.be1.shared.messaging.InboxCoordinator;
import com.agridrone.be1.shared.messaging.JdbcInboxRepository;
import com.agridrone.be1.shared.messaging.JdbcOutboxRepository;
import com.agridrone.be1.shared.messaging.IntegrationEventEnvelope;
import com.agridrone.be1.shared.messaging.IntegrationEventHandler;
import com.agridrone.be1.shared.messaging.IntegrationEventSerializer;
import com.agridrone.be1.shared.messaging.MessagingProperties;
import com.agridrone.be1.shared.messaging.OutboxDispatcher;
import com.agridrone.be1.shared.messaging.OutboxService;
import com.agridrone.be1.shared.idempotency.IdempotencyResult;
import com.agridrone.be1.shared.idempotency.IdempotencyStore;
import com.agridrone.be1.shared.idempotency.JdbcIdempotencyRepository;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.micrometer.core.instrument.simple.SimpleMeterRegistry;
import java.time.Duration;
import java.time.OffsetDateTime;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicInteger;
import org.flywaydb.core.Flyway;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.datasource.DriverManagerDataSource;
import org.springframework.jdbc.support.JdbcTransactionManager;
import org.springframework.transaction.support.TransactionTemplate;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.utility.DockerImageName;

@Testcontainers(disabledWithoutDocker = true)
class SharedRuntimeIT {
    @Container
    static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>(
            DockerImageName.parse("postgis/postgis:17-3.5").asCompatibleSubstituteFor("postgres"));

    static JdbcTemplate jdbc;
    static TransactionTemplate transactions;
    static ObjectMapper mapper;
    static IntegrationEventSerializer serializer;

    @BeforeAll
    static void initialize() {
        Flyway.configure().dataSource(POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword())
                .locations("classpath:db/migration").load().migrate();
        var dataSource = new DriverManagerDataSource(POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword());
        jdbc = new JdbcTemplate(dataSource);
        transactions = new TransactionTemplate(new JdbcTransactionManager(dataSource));
        mapper = new ObjectMapper().findAndRegisterModules();
        serializer = new IntegrationEventSerializer(mapper);
    }

    @BeforeEach
    void cleanRuntimeTables() {
        jdbc.execute("TRUNCATE messaging.idempotency_records, messaging.inbox_messages, messaging.outbox_messages, audit.audit_logs");
    }

    @Test
    void businessRollbackAlsoRollsBackAuditAndOutbox() {
        var audit = new AuditWriter(new JdbcAuditRepository(jdbc), mapper);
        var outbox = new OutboxService(new JdbcOutboxRepository(jdbc, transactions), serializer);
        IntegrationEventEnvelope event = event();
        try (var ignored = ExecutionContext.begin(context(event))) {
            assertThatThrownBy(() -> transactions.executeWithoutResult(status -> {
                audit.append(new AuditEntry(null, "Tenant", event.tenantId(), "CREATE", null,
                        mapper.createObjectNode().put("name", "rollback"), "test rollback"));
                outbox.enqueue(event, event.eventType(), event.tenantId().toString());
                throw new IllegalStateException("business failure");
            })).isInstanceOf(IllegalStateException.class);
        }
        assertThat(count("audit.audit_logs")).isZero();
        assertThat(count("messaging.outbox_messages")).isZero();
    }

    @Test
    void crashBeforeCommitRetriesAndCrashAfterCommitIsDeduplicated() {
        InboxCoordinator inbox = new InboxCoordinator(new JdbcInboxRepository(jdbc), transactions);
        IntegrationEventEnvelope event = event();
        AtomicInteger invocations = new AtomicInteger();
        IntegrationEventHandler crashing = handler(event.eventType(), value -> {
            invocations.incrementAndGet();
            throw new IllegalStateException("simulated crash before commit");
        });
        assertThatThrownBy(() -> inbox.process("phase3", event, crashing)).isInstanceOf(IllegalStateException.class);
        assertThat(count("messaging.inbox_messages")).isZero();

        IntegrationEventHandler succeeds = handler(event.eventType(), value -> invocations.incrementAndGet());
        assertThat(inbox.process("phase3", event, succeeds)).isEqualTo(InboxCoordinator.Result.COMPLETED);
        assertThat(inbox.process("phase3", event, succeeds)).isEqualTo(InboxCoordinator.Result.DUPLICATE);
        assertThat(invocations).hasValue(2);
        assertThat(count("messaging.inbox_messages")).isEqualTo(1);
    }

    @Test
    void brokerOutageKeepsCommittedEventAndSchedulesBoundedRetry() {
        IntegrationEventEnvelope event = event();
        var outbox = new OutboxService(new JdbcOutboxRepository(jdbc, transactions), serializer);
        transactions.executeWithoutResult(status -> outbox.enqueue(event, event.eventType(), null));
        var repository = new JdbcOutboxRepository(jdbc, transactions);
        MessagingProperties properties = properties();
        ConfirmedEventPublisher unavailable = (exchange, key, body, headers) -> {
            throw new IllegalStateException("broker unavailable");
        };
        new OutboxDispatcher(repository, unavailable, properties, new SimpleMeterRegistry()).dispatch();

        assertThat(count("messaging.outbox_messages")).isEqualTo(1);
        Map<String, Object> state = jdbc.queryForMap("""
                SELECT status, attempt_count, next_attempt_at IS NOT NULL AS scheduled
                FROM messaging.outbox_messages WHERE message_id=?
                """, event.messageId());
        assertThat(state.get("status")).isEqualTo("RETRY");
        assertThat(state.get("attempt_count")).isEqualTo(1);
        assertThat(state.get("scheduled")).isEqualTo(true);
    }

    @Test
    void idempotencyReplaysSameRequestAndRejectsKeyReuseWithDifferentPayload() {
        IdempotencyStore store = new IdempotencyStore(new JdbcIdempotencyRepository(jdbc), mapper);
        assertThat(store.begin("http:create-farm", "key-1", "one".getBytes(), Duration.ofHours(1)).state())
                .isEqualTo(IdempotencyResult.State.ACQUIRED);
        store.complete("http:create-farm", "key-1", 201, mapper.createObjectNode().put("id", "farm-1"));
        IdempotencyResult replay = store.begin(
                "http:create-farm", "key-1", "one".getBytes(), Duration.ofHours(1));
        assertThat(replay.state()).isEqualTo(IdempotencyResult.State.REPLAY);
        assertThat(replay.responseStatus()).isEqualTo(201);
        assertThatThrownBy(() -> store.begin(
                "http:create-farm", "key-1", "different".getBytes(), Duration.ofHours(1)))
                .hasMessageContaining("different request");
    }

    private static IntegrationEventHandler handler(String type, java.util.function.Consumer<IntegrationEventEnvelope> action) {
        return new IntegrationEventHandler() {
            public String eventType() { return type; }
            public void handle(IntegrationEventEnvelope event) { action.accept(event); }
        };
    }

    private static long count(String table) {
        return jdbc.queryForObject("SELECT count(*) FROM " + table, Long.class);
    }

    private static IntegrationEventEnvelope event() {
        return new IntegrationEventEnvelope(UUID.randomUUID(), UUID.randomUUID(), UUID.randomUUID(), null,
                OffsetDateTime.now().minusSeconds(1), 1, "notification.email-requested.v1",
                mapper.createObjectNode().put("notificationId", UUID.randomUUID().toString()));
    }

    private static ExecutionContextSnapshot context(IntegrationEventEnvelope event) {
        return new ExecutionContextSnapshot(event.tenantId(), null, ActorType.SYSTEM, event.correlationId(),
                event.messageId(), ExecutionSource.SYSTEM, Set.of());
    }

    private static MessagingProperties properties() {
        return new MessagingProperties(true, "agridrone.events", "agridrone.retry", "agridrone.dead-letter",
                new MessagingProperties.Outbox(50, Duration.ofSeconds(30), Duration.ofSeconds(1),
                        Duration.ofSeconds(5), 3, Duration.ofMillis(10), Duration.ofSeconds(1)), Map.of());
    }
}

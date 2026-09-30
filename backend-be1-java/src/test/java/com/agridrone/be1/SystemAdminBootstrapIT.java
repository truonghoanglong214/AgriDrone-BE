package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;

import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminCommand;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminResult;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminUseCase;
import com.agridrone.be1.identity.application.service.SystemAdminBootstrapService;
import com.agridrone.be1.identity.infrastructure.initialization.JpaSystemAdminBootstrapLock;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.RolePersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter.UserPersistenceAdapter;
import com.agridrone.be1.identity.infrastructure.security.BCryptPasswordHasher;
import com.agridrone.be1.identity.infrastructure.security.PasswordSecurityConfiguration;
import com.agridrone.be1.shared.execution.PlatformConfiguration;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.utility.DockerImageName;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=none")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import({
        SystemAdminBootstrapService.class,
        JpaSystemAdminBootstrapLock.class,
        UserPersistenceAdapter.class,
        RolePersistenceAdapter.class,
        BCryptPasswordHasher.class,
        PasswordSecurityConfiguration.class,
        PlatformConfiguration.class
})
@Testcontainers(disabledWithoutDocker = true)
@Transactional(propagation = Propagation.NOT_SUPPORTED)
class SystemAdminBootstrapIT {
    private static final Duration WAIT_TIMEOUT = Duration.ofSeconds(10);
    private static final BootstrapSystemAdminCommand COMMAND =
            new BootstrapSystemAdminCommand(
                    "admin@example.com",
                    "System Administrator");

    @Container
    static final PostgreSQLContainer<?> DATABASE = new PostgreSQLContainer<>(
            DockerImageName.parse("postgis/postgis:17-3.5")
                    .asCompatibleSubstituteFor("postgres"));

    @DynamicPropertySource
    static void databaseProperties(DynamicPropertyRegistry registry) {
        registry.add("spring.datasource.url", DATABASE::getJdbcUrl);
        registry.add("spring.datasource.username", DATABASE::getUsername);
        registry.add("spring.datasource.password", DATABASE::getPassword);
        registry.add("spring.flyway.enabled", () -> true);
        registry.add("agridrone.runtime.enabled", () -> true);
    }

    @Autowired
    BootstrapSystemAdminUseCase bootstrap;

    @Autowired
    JdbcTemplate jdbc;

    @BeforeEach
    void cleanUsers() {
        jdbc.execute("TRUNCATE identity.user_roles, identity.users CASCADE");
    }

    @Test
    void repeatedBootstrapIsIdempotent() {
        BootstrapSystemAdminResult first = bootstrap.bootstrap(COMMAND);
        BootstrapSystemAdminResult second = bootstrap.bootstrap(COMMAND);

        assertThat(first.created()).isTrue();
        assertThat(second.created()).isFalse();
        assertThat(userCount()).isOne();
        assertThat(userRoleCount()).isOne();
    }

    @Test
    void concurrentBootstrapCreatesExactlyOneAdmin() throws Exception {
        ExecutorService executor = Executors.newFixedThreadPool(2);
        CountDownLatch ready = new CountDownLatch(2);
        CountDownLatch start = new CountDownLatch(1);

        try {
            Future<BootstrapSystemAdminResult> first = executor.submit(() ->
                    runAfterBarrier(ready, start));
            Future<BootstrapSystemAdminResult> second = executor.submit(() ->
                    runAfterBarrier(ready, start));

            assertThat(ready.await(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
            start.countDown();

            List<BootstrapSystemAdminResult> results = List.of(
                    first.get(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS),
                    second.get(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS));

            assertThat(results).filteredOn(BootstrapSystemAdminResult::created).hasSize(1);
            assertThat(results).filteredOn(result -> !result.created()).hasSize(1);
            assertThat(userCount()).isOne();
            assertThat(userRoleCount()).isOne();
        } finally {
            start.countDown();
            executor.shutdownNow();
        }
    }

    private BootstrapSystemAdminResult runAfterBarrier(
            CountDownLatch ready,
            CountDownLatch start) throws InterruptedException {
        ready.countDown();
        if (!start.await(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS)) {
            throw new IllegalStateException("Timed out waiting to start bootstrap");
        }
        return bootstrap.bootstrap(COMMAND);
    }

    private long userCount() {
        return jdbc.queryForObject("SELECT count(*) FROM identity.users", Long.class);
    }

    private long userRoleCount() {
        return jdbc.queryForObject("SELECT count(*) FROM identity.user_roles", Long.class);
    }
}

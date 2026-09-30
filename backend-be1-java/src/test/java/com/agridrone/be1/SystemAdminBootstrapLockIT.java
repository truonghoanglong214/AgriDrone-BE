package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.agridrone.be1.identity.application.port.out.initialization.SystemAdminBootstrapLock;
import com.agridrone.be1.identity.infrastructure.initialization.JpaSystemAdminBootstrapLock;
import java.time.Duration;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.TransactionDefinition;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionTemplate;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.utility.DockerImageName;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=none")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import(JpaSystemAdminBootstrapLock.class)
@Testcontainers(disabledWithoutDocker = true)
@Transactional(propagation = Propagation.NOT_SUPPORTED)
class SystemAdminBootstrapLockIT {

    private static final Duration WAIT_TIMEOUT = Duration.ofSeconds(5);

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
    }

    @Autowired
    SystemAdminBootstrapLock bootstrapLock;

    @Autowired
    PlatformTransactionManager transactionManager;

    @Test
    void requiresAnExistingTransaction() {
        assertThatThrownBy(bootstrapLock::acquire)
                .isInstanceOf(org.springframework.transaction.IllegalTransactionStateException.class);
    }

    @Test
    void acquiresTheSeededLockInsideTransaction() {
        newTransaction().executeWithoutResult(status -> bootstrapLock.acquire());
    }

    @Test
    void serializesConcurrentBootstrapTransactions() throws Exception {
        ExecutorService executor = Executors.newFixedThreadPool(2);
        CountDownLatch firstHasLock = new CountDownLatch(1);
        CountDownLatch releaseFirst = new CountDownLatch(1);
        CountDownLatch secondEnteredTransaction = new CountDownLatch(1);

        Future<?> first = executor.submit(() -> newTransaction().executeWithoutResult(status -> {
            bootstrapLock.acquire();
            firstHasLock.countDown();
            await(releaseFirst);
        }));

        try {
            assertThat(firstHasLock.await(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS))
                    .isTrue();

            Future<?> second = executor.submit(() -> newTransaction().executeWithoutResult(status -> {
                secondEnteredTransaction.countDown();
                bootstrapLock.acquire();
            }));

            assertThat(secondEnteredTransaction.await(
                    WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS)).isTrue();
            assertStillWaiting(second);

            releaseFirst.countDown();
            first.get(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS);
            second.get(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS);
        } finally {
            releaseFirst.countDown();
            executor.shutdownNow();
        }
    }

    private TransactionTemplate newTransaction() {
        TransactionTemplate transaction = new TransactionTemplate(transactionManager);
        transaction.setPropagationBehavior(TransactionDefinition.PROPAGATION_REQUIRES_NEW);
        return transaction;
    }

    private static void assertStillWaiting(Future<?> future) {
        assertThatThrownBy(() -> future.get(300, TimeUnit.MILLISECONDS))
                .isInstanceOf(TimeoutException.class);
    }

    private static void await(CountDownLatch latch) {
        try {
            if (!latch.await(WAIT_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS)) {
                throw new IllegalStateException("Timed out waiting to release bootstrap lock");
            }
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Interrupted while holding bootstrap lock", exception);
        }
    }
}

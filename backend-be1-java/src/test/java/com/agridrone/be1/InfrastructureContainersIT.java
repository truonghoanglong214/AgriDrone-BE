package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;

import com.rabbitmq.client.ConnectionFactory;
import io.lettuce.core.RedisClient;
import java.sql.DriverManager;
import org.junit.jupiter.api.Test;
import org.testcontainers.containers.GenericContainer;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.containers.RabbitMQContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.utility.DockerImageName;

@Testcontainers(disabledWithoutDocker = true)
class InfrastructureContainersIT {

    @Container
    static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>(
            DockerImageName.parse("postgis/postgis:17-3.5")
                    .asCompatibleSubstituteFor("postgres"));

    @Container
    static final RabbitMQContainer RABBITMQ =
            new RabbitMQContainer("rabbitmq:4.2.7-management-alpine");

    @Container
    static final GenericContainer<?> REDIS =
            new GenericContainer<>(DockerImageName.parse("redis:8.8.1-alpine"))
                    .withExposedPorts(6379);

    @Test
    void postgisRabbitMqAndRedisAreUsable() throws Exception {
        try (var connection = DriverManager.getConnection(
                    POSTGRES.getJdbcUrl(),
                    POSTGRES.getUsername(),
                    POSTGRES.getPassword());
             var statement = connection.createStatement();
             var result = statement.executeQuery("SELECT PostGIS_Version()")) {
            assertThat(result.next()).isTrue();
            assertThat(result.getString(1)).isNotBlank();
        }

        ConnectionFactory rabbitConnectionFactory = new ConnectionFactory();
        rabbitConnectionFactory.setUri(RABBITMQ.getAmqpUrl());
        try (var connection = rabbitConnectionFactory.newConnection()) {
            assertThat(connection.isOpen()).isTrue();
        }

        RedisClient redisClient = RedisClient.create(
                "redis://" + REDIS.getHost() + ":" + REDIS.getMappedPort(6379));
        try (var connection = redisClient.connect()) {
            assertThat(connection.sync().ping()).isEqualTo("PONG");
        } finally {
            redisClient.shutdown();
        }
    }
}

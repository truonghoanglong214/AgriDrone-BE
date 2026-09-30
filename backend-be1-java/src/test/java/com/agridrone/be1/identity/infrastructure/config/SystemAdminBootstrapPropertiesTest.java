package com.agridrone.be1.identity.infrastructure.config;

import static org.assertj.core.api.Assertions.assertThat;

import jakarta.validation.Validation;
import jakarta.validation.Validator;
import jakarta.validation.ConstraintViolation;
import java.util.Set;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.boot.test.context.runner.ApplicationContextRunner;
import org.springframework.context.annotation.Configuration;

class SystemAdminBootstrapPropertiesTest {
    private static Validator validator;
    private final ApplicationContextRunner contextRunner = new ApplicationContextRunner()
            .withUserConfiguration(PropertiesConfiguration.class);

    @BeforeAll
    static void createValidator() {
        validator = Validation.buildDefaultValidatorFactory().getValidator();
    }

    @Test
    void allowsEmptyValuesWhenBootstrapIsDisabled() {
        SystemAdminBootstrapProperties properties = new SystemAdminBootstrapProperties();
        properties.setEnabled(false);
        properties.setEmail("");
        properties.setFullName("");

        assertThat(validator.validate(properties)).isEmpty();
    }

    @Test
    void acceptsValidEnabledConfiguration() {
        SystemAdminBootstrapProperties properties = new SystemAdminBootstrapProperties();
        properties.setEnabled(true);
        properties.setEmail("admin@example.com");
        properties.setFullName("System Administrator");

        assertThat(validator.validate(properties)).isEmpty();
    }

    @Test
    void rejectsDisplayAddressAndBlankName() {
        SystemAdminBootstrapProperties properties = new SystemAdminBootstrapProperties();
        properties.setEnabled(true);
        properties.setEmail("Admin <admin@example.com>");
        properties.setFullName(" ");

        assertThat(validator.validate(properties)).hasSize(2);
    }

    @Test
    void rejectsOversizedValues() {
        SystemAdminBootstrapProperties properties = new SystemAdminBootstrapProperties();
        properties.setEnabled(true);
        properties.setEmail("a".repeat(310) + "@example.com");
        properties.setFullName("a".repeat(151));

        Set<ConstraintViolation<SystemAdminBootstrapProperties>> violations =
                validator.validate(properties);

        assertThat(violations)
                .extracting(violation -> violation.getPropertyPath().toString())
                .contains("email", "fullName");
    }

    @Test
    void invalidEnabledConfigurationFailsApplicationContext() {
        contextRunner
                .withPropertyValues(
                        "agridrone.identity.system-admin-bootstrap.enabled=true",
                        "agridrone.identity.system-admin-bootstrap.email=invalid",
                        "agridrone.identity.system-admin-bootstrap.full-name=")
                .run(context -> {
                    assertThat(context).hasFailed();
                    assertThat(context.getStartupFailure())
                            .hasMessageContaining("system-admin-bootstrap");
                });
    }

    @Configuration(proxyBeanMethods = false)
    @EnableConfigurationProperties(SystemAdminBootstrapProperties.class)
    static class PropertiesConfiguration {
    }
}

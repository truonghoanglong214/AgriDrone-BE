package com.agridrone.be1.identity.infrastructure.initialization;

import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminCommand;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminResult;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminUseCase;
import com.agridrone.be1.identity.infrastructure.config.SystemAdminBootstrapProperties;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(
        prefix = "agridrone.identity.system-admin-bootstrap",
        name = "enabled",
        havingValue = "true")
public class SystemAdminBootstrapInitializer implements ApplicationRunner {
    private static final Logger LOGGER =
            LoggerFactory.getLogger(SystemAdminBootstrapInitializer.class);

    private final BootstrapSystemAdminUseCase bootstrapSystemAdmin;
    private final SystemAdminBootstrapProperties properties;

    public SystemAdminBootstrapInitializer(
            BootstrapSystemAdminUseCase bootstrapSystemAdmin,
            SystemAdminBootstrapProperties properties) {
        this.bootstrapSystemAdmin = bootstrapSystemAdmin;
        this.properties = properties;
    }

    @Override
    public void run(ApplicationArguments args) {
        BootstrapSystemAdminResult result = bootstrapSystemAdmin.bootstrap(
                new BootstrapSystemAdminCommand(
                        properties.getEmail(),
                        properties.getFullName()));

        if (result.created()) {
            LOGGER.info(
                    "Created the initial System Admin account userId={} email={}",
                    result.userId(),
                    result.email());
        } else {
            LOGGER.info("System Admin bootstrap skipped because an active System Admin already exists");
        }
    }
}

package com.agridrone.be1.identity.infrastructure.initialization;

import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminCommand;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminResult;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminUseCase;
import com.agridrone.be1.identity.infrastructure.config.SystemAdminBootstrapProperties;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.boot.DefaultApplicationArguments;

class SystemAdminBootstrapInitializerTest {

    @Test
    void invokesBootstrapWithConfiguredIdentity() {
        BootstrapSystemAdminUseCase useCase = org.mockito.Mockito.mock(
                BootstrapSystemAdminUseCase.class);
        SystemAdminBootstrapProperties properties = new SystemAdminBootstrapProperties();
        properties.setEnabled(true);
        properties.setEmail("admin@example.com");
        properties.setFullName("System Administrator");
        when(useCase.bootstrap(new BootstrapSystemAdminCommand(
                properties.getEmail(),
                properties.getFullName())))
                .thenReturn(new BootstrapSystemAdminResult(
                        true,
                        UUID.randomUUID(),
                        properties.getEmail()));

        new SystemAdminBootstrapInitializer(useCase, properties)
                .run(new DefaultApplicationArguments());

        verify(useCase).bootstrap(new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator"));
    }
}

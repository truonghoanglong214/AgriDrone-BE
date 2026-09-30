package com.agridrone.be1.shared.execution;

import java.time.Clock;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration(proxyBeanMethods = false)
public class PlatformConfiguration {

    @Bean
    Clock utcClock() {
        return Clock.systemUTC();
    }
}

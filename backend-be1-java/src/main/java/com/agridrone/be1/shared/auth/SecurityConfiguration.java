package com.agridrone.be1.shared.auth;

import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.HttpStatus;
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.oauth2.server.resource.web.authentication.BearerTokenAuthenticationFilter;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationConverter;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.beans.factory.ObjectProvider;
import com.agridrone.be1.shared.execution.ExecutionContextFilter;

@Configuration(proxyBeanMethods = false)
@EnableMethodSecurity
public class SecurityConfiguration {

    private static final String[] PUBLIC_PLATFORM_ENDPOINTS = {
        "/actuator/health",
        "/actuator/health/**",
        "/.well-known/jwks.json",
        "/v3/api-docs",
        "/v3/api-docs/**",
        "/swagger-ui.html",
        "/swagger-ui/**",
        "/webjars/**",
        "/api/auth/login",
        "/api/auth/select-tenant",
        "/api/auth/invitations/preview",
        "/api/auth/system-manager-invitations/preview",
        "/api/auth/system-manager-invitations/accept",
        "/api/auth/forgot-password",
        "/api/auth/reset-password",
        "/invitations/accept",
        "/error"
    };

    @Bean
    @ConditionalOnProperty(prefix = "agridrone.security.jwt", name = "enabled", havingValue = "true")
    SecurityFilterChain jwtSecurityFilterChain(
            HttpSecurity http,
            SecurityErrorWriter errorWriter,
            ObjectProvider<EffectiveTenantAccessGuard> tenantAccessGuard) throws Exception {
        configureCommon(http, errorWriter)
                .authorizeHttpRequests(authorize -> authorize
                        .requestMatchers(PUBLIC_PLATFORM_ENDPOINTS).permitAll()
                        .anyRequest().authenticated())
                .oauth2ResourceServer(oauth -> oauth.jwt(jwt -> {
                    JwtAuthenticationConverter converter = new JwtAuthenticationConverter();
                    converter.setJwtGrantedAuthoritiesConverter(new JwtAuthoritiesConverter());
                    jwt.jwtAuthenticationConverter(converter);
                }));
        http.addFilterAfter(new ExecutionContextFilter(),
                BearerTokenAuthenticationFilter.class);
        EffectiveTenantAccessGuard guard = tenantAccessGuard.getIfAvailable();
        if (guard != null) {
            http.addFilterAfter(
                    new EffectiveTenantAccessFilter(guard, errorWriter),
                    BearerTokenAuthenticationFilter.class);
        }
        return http.build();
    }

    @Bean
    @ConditionalOnProperty(
            prefix = "agridrone.security.jwt",
            name = "enabled",
            havingValue = "false",
            matchIfMissing = true)
    SecurityFilterChain closedSecurityFilterChain(
            HttpSecurity http,
            SecurityErrorWriter errorWriter) throws Exception {
        configureCommon(http, errorWriter)
                .authorizeHttpRequests(authorize -> authorize
                        .requestMatchers(PUBLIC_PLATFORM_ENDPOINTS).permitAll()
                        .anyRequest().denyAll());
        return http.build();
    }

    private static HttpSecurity configureCommon(
            HttpSecurity http,
            SecurityErrorWriter errorWriter) throws Exception {
        return http
                .csrf(csrf -> csrf.disable())
                .sessionManagement(session ->
                        session.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
                .requestCache(cache -> cache.disable())
                .formLogin(form -> form.disable())
                .httpBasic(basic -> basic.disable())
                .exceptionHandling(errors -> errors
                        .authenticationEntryPoint((request, response, exception) -> errorWriter.write(
                                request,
                                response,
                                HttpStatus.UNAUTHORIZED.value(),
                                "Auth.AuthenticationRequired",
                                "Authentication is required."))
                        .accessDeniedHandler((request, response, exception) -> errorWriter.write(
                                request,
                                response,
                                HttpStatus.FORBIDDEN.value(),
                                "Auth.AccessDenied",
                                "Access is denied.")));
    }
}

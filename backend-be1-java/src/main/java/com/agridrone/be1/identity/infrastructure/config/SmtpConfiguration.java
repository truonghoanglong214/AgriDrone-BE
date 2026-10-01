package com.agridrone.be1.identity.infrastructure.config;

import java.util.Properties;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.mail.javamail.JavaMailSenderImpl;
import org.springframework.util.StringUtils;

@Configuration(proxyBeanMethods = false)
@ConditionalOnProperty(
        prefix = "agridrone.notification.smtp",
        name = "enabled",
        havingValue = "true")
public class SmtpConfiguration {

    @Bean
    JavaMailSender javaMailSender(SmtpProperties properties) {
        require(properties.host(), "BE1_SMTP_HOST");
        require(properties.fromAddress(), "BE1_SMTP_FROM_ADDRESS");
        if (properties.port() <= 0 || properties.port() > 65535) {
            throw new IllegalStateException("BE1_SMTP_PORT must be between 1 and 65535.");
        }
        if (properties.timeoutSeconds() <= 0) {
            throw new IllegalStateException("BE1_SMTP_TIMEOUT_SECONDS must be positive.");
        }

        JavaMailSenderImpl sender = new JavaMailSenderImpl();
        sender.setHost(properties.host());
        sender.setPort(properties.port());
        sender.setUsername(properties.username());
        sender.setPassword(properties.password());
        Properties javaMail = sender.getJavaMailProperties();
        javaMail.put("mail.smtp.auth", String.valueOf(StringUtils.hasText(properties.username())));
        String securityMode = properties.securityMode() == null
                ? "STARTTLS" : properties.securityMode().trim().toUpperCase();
        switch (securityMode) {
            case "NONE" -> javaMail.put("mail.smtp.starttls.enable", "false");
            case "STARTTLS" -> javaMail.put("mail.smtp.starttls.enable", "true");
            case "SSL" -> javaMail.put("mail.smtp.ssl.enable", "true");
            default -> throw new IllegalStateException(
                    "BE1_SMTP_SECURITY_MODE must be NONE, STARTTLS, or SSL.");
        }
        javaMail.put("mail.smtp.connectiontimeout", properties.timeoutSeconds() * 1000);
        javaMail.put("mail.smtp.timeout", properties.timeoutSeconds() * 1000);
        javaMail.put("mail.smtp.writetimeout", properties.timeoutSeconds() * 1000);
        return sender;
    }

    private static void require(String value, String variable) {
        if (!StringUtils.hasText(value)) {
            throw new IllegalStateException(variable + " is required when SMTP is enabled.");
        }
    }
}

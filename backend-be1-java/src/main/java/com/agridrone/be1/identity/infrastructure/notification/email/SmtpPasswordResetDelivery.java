package com.agridrone.be1.identity.infrastructure.notification.email;

import com.agridrone.be1.identity.application.port.out.notification.PasswordResetDelivery;
import com.agridrone.be1.identity.infrastructure.config.SmtpProperties;
import com.agridrone.be1.identity.infrastructure.config.PasswordResetProperties;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.mail.SimpleMailMessage;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(
        prefix = "agridrone.notification.smtp",
        name = "enabled",
        havingValue = "true")
public class SmtpPasswordResetDelivery implements PasswordResetDelivery {
    private static final DateTimeFormatter UTC = DateTimeFormatter.ISO_INSTANT
            .withZone(ZoneOffset.UTC);

    private final JavaMailSender mailSender;
    private final SmtpProperties smtp;
    private final PasswordResetProperties reset;

    public SmtpPasswordResetDelivery(
            JavaMailSender mailSender,
            SmtpProperties smtp,
            PasswordResetProperties reset) {
        this.mailSender = mailSender;
        this.smtp = smtp;
        this.reset = reset;
    }

    @Override
    public void send(String email, String fullName, String plainTextToken, Instant expiresAt) {
        String separator = reset.resetUrl().contains("?") ? "&" : "?";
        String link = reset.resetUrl() + separator + "token="
                + URLEncoder.encode(plainTextToken, StandardCharsets.UTF_8);
        SimpleMailMessage message = new SimpleMailMessage();
        message.setFrom(smtp.fromAddress());
        message.setTo(email);
        message.setSubject("Reset your AgriDrone password");
        message.setText("Hello " + fullName + ",\n\n"
                + "Use the following link to reset your password:\n"
                + link + "\n\n"
                + "This link expires at " + UTC.format(expiresAt) + ".\n");
        mailSender.send(message);
    }
}

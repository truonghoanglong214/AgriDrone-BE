package com.agridrone.be1.identity.infrastructure.notification.email;

import com.agridrone.be1.identity.application.port.out.notification.SystemManagerInvitationDelivery;
import com.agridrone.be1.identity.infrastructure.config.SmtpProperties;
import com.agridrone.be1.identity.infrastructure.config.SystemManagerInvitationProperties;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
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
public class SmtpSystemManagerInvitationDelivery
        implements SystemManagerInvitationDelivery {
    private final JavaMailSender mailSender;
    private final SmtpProperties smtp;
    private final SystemManagerInvitationProperties invitation;

    public SmtpSystemManagerInvitationDelivery(
            JavaMailSender mailSender,
            SmtpProperties smtp,
            SystemManagerInvitationProperties invitation) {
        this.mailSender = mailSender;
        this.smtp = smtp;
        this.invitation = invitation;
    }

    @Override
    public void send(String email, String plainTextToken, Instant expiresAt) {
        String separator = invitation.acceptUrl().contains("?") ? "&" : "?";
        String actionUrl = invitation.acceptUrl() + separator + "token="
                + URLEncoder.encode(plainTextToken, StandardCharsets.UTF_8);
        SimpleMailMessage message = new SimpleMailMessage();
        message.setFrom(smtp.fromAddress());
        message.setTo(email);
        message.setSubject("AgriDrone System Manager invitation");
        message.setText("Hello " + email + ",\n\n"
                + "A System Administrator invited you to join AgriDrone "
                + "as a System Manager.\n"
                + "Accept invitation: " + actionUrl + "\n"
                + "Expires at: " + DateTimeFormatter.ISO_INSTANT.format(expiresAt));
        mailSender.send(message);
    }
}

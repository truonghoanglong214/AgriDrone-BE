package com.agridrone.be1.shared.api;

import com.agridrone.be1.shared.messaging.MessagingRecoveryService;
import java.util.Map;
import java.util.UUID;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@ConditionalOnProperty(name = "agridrone.messaging.enabled", havingValue = "true")
@RequestMapping("/api/system/messaging")
@PreAuthorize("hasAuthority('SYSTEM_ADMIN')")
public class MessagingOperationsController {
    private final MessagingRecoveryService recovery;

    public MessagingOperationsController(MessagingRecoveryService recovery) {
        this.recovery = recovery;
    }

    @PostMapping("/outbox/{messageId}/redrive")
    Map<String, Object> redriveOutbox(@PathVariable UUID messageId, @RequestBody(required = false) RedriveRequest request) {
        recovery.redriveOutbox(messageId, request == null ? "manual redrive" : request.reason());
        return Map.of("messageId", messageId, "redriven", true);
    }

    @PostMapping("/dead-letters/{consumerName}/redrive")
    Map<String, Object> redriveDeadLetter(@PathVariable String consumerName) {
        return Map.of("consumerName", consumerName, "redriven", recovery.redriveDeadLetter(consumerName));
    }

    public record RedriveRequest(String reason) { }
}

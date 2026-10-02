package com.agridrone.be1.identity.api;

import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.error.TenantInvitationErrorCodes;
import com.agridrone.be1.shared.error.ApiErrorResponse;
import com.agridrone.be1.shared.execution.CorrelationIds;
import jakarta.servlet.http.HttpServletRequest;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Locale;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@RestControllerAdvice
public class IdentityConstraintExceptionHandler {
    private final Clock clock;

    public IdentityConstraintExceptionHandler(Clock clock) {
        this.clock = clock;
    }

    @ExceptionHandler(DataIntegrityViolationException.class)
    ResponseEntity<ApiErrorResponse> handle(
            DataIntegrityViolationException exception,
            HttpServletRequest request) {
        String details = exceptionDetails(exception);
        if (details.contains("uq_tenant_invitations_pending_owner_provisioning")
                || details.contains("uq_tenant_invitations_pending_tenant_email")) {
            return response(
                    HttpStatus.CONFLICT,
                    TenantInvitationErrorCodes.OWNER_PROVISIONING_ALREADY_PENDING,
                    "The tenant already has an active owner provisioning request.",
                    request);
        }
        if (details.contains("uq_tenant_memberships_active_owner")) {
            return response(
                    HttpStatus.CONFLICT,
                    TenantInvitationErrorCodes.OWNER_ALREADY_ASSIGNED,
                    "The tenant already has an active owner.",
                    request);
        }
        if (details.contains("uq_tenant_memberships_tenant_user")) {
            return response(
                    HttpStatus.CONFLICT,
                    TenantInvitationErrorCodes.USER_ALREADY_MEMBER,
                    "The invited user is already a member of the current tenant.",
                    request);
        }
        if (details.contains("ux_tenants_code_active")) {
            return response(
                    HttpStatus.CONFLICT,
                    TenantErrorCodes.CODE_ALREADY_EXISTS,
                    "A tenant with this code already exists.",
                    request);
        }
        return response(
                HttpStatus.INTERNAL_SERVER_ERROR,
                "Server.UnexpectedError",
                "An unexpected error occurred.",
                request);
    }

    private ResponseEntity<ApiErrorResponse> response(
            HttpStatus status,
            String code,
            String message,
            HttpServletRequest request) {
        return ResponseEntity.status(status).body(new ApiErrorResponse(
                Instant.now(clock),
                status.value(),
                code,
                message,
                CorrelationIds.currentOrCreate(),
                request.getRequestURI(),
                List.of()));
    }

    private static String exceptionDetails(Throwable exception) {
        StringBuilder details = new StringBuilder();
        Throwable current = exception;
        while (current != null) {
            if (current.getMessage() != null) {
                details.append(' ').append(current.getMessage());
            }
            current = current.getCause();
        }
        return details.toString().toLowerCase(Locale.ROOT);
    }
}

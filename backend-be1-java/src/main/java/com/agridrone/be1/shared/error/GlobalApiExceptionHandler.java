package com.agridrone.be1.shared.error;

import com.agridrone.be1.shared.execution.CorrelationIds;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.ConstraintViolationException;
import java.time.Clock;
import java.time.Instant;
import java.util.Comparator;
import java.util.List;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.servlet.resource.NoResourceFoundException;

@RestControllerAdvice
public class GlobalApiExceptionHandler {

    private static final Logger LOGGER = LoggerFactory.getLogger(GlobalApiExceptionHandler.class);
    private final Clock clock;

    public GlobalApiExceptionHandler(Clock clock) {
        this.clock = clock;
    }

    @ExceptionHandler(StableApiException.class)
    ResponseEntity<ApiErrorResponse> handleStableApiException(
            StableApiException exception,
            HttpServletRequest request) {
        return response(
                exception.status(),
                exception.code(),
                exception.getMessage(),
                request,
                List.of());
    }

    @ExceptionHandler(MethodArgumentNotValidException.class)
    ResponseEntity<ApiErrorResponse> handleBodyValidation(
            MethodArgumentNotValidException exception,
            HttpServletRequest request) {
        List<ApiErrorResponse.Violation> violations = exception.getBindingResult()
                .getFieldErrors()
                .stream()
                .sorted(Comparator.comparing(FieldError::getField))
                .map(error -> new ApiErrorResponse.Violation(
                        error.getField(),
                        error.getCode() == null ? "Invalid" : error.getCode(),
                        error.getDefaultMessage() == null ? "Invalid value." : error.getDefaultMessage()))
                .toList();

        return response(
                HttpStatus.UNPROCESSABLE_ENTITY,
                "Validation.Failed",
                "One or more validation errors occurred.",
                request,
                violations);
    }

    @ExceptionHandler(ConstraintViolationException.class)
    ResponseEntity<ApiErrorResponse> handleConstraintValidation(
            ConstraintViolationException exception,
            HttpServletRequest request) {
        List<ApiErrorResponse.Violation> violations = exception.getConstraintViolations()
                .stream()
                .map(violation -> new ApiErrorResponse.Violation(
                        violation.getPropertyPath().toString(),
                        "ConstraintViolation",
                        violation.getMessage()))
                .sorted(Comparator.comparing(ApiErrorResponse.Violation::field))
                .toList();

        return response(
                HttpStatus.UNPROCESSABLE_ENTITY,
                "Validation.Failed",
                "One or more validation errors occurred.",
                request,
                violations);
    }

    @ExceptionHandler(HttpMessageNotReadableException.class)
    ResponseEntity<ApiErrorResponse> handleUnreadableRequest(HttpServletRequest request) {
        return response(
                HttpStatus.BAD_REQUEST,
                "Request.InvalidJson",
                "The request body is not valid JSON.",
                request,
                List.of());
    }

    @ExceptionHandler(AccessDeniedException.class)
    ResponseEntity<ApiErrorResponse> handleAccessDenied(HttpServletRequest request) {
        return response(
                HttpStatus.FORBIDDEN,
                "Auth.AccessDenied",
                "Access is denied.",
                request,
                List.of());
    }

    @ExceptionHandler(NoResourceFoundException.class)
    ResponseEntity<ApiErrorResponse> handleNotFound(HttpServletRequest request) {
        return response(
                HttpStatus.NOT_FOUND,
                "Resource.NotFound",
                "The requested resource was not found.",
                request,
                List.of());
    }

    @ExceptionHandler(Exception.class)
    ResponseEntity<ApiErrorResponse> handleUnexpected(
            Exception exception,
            HttpServletRequest request) {
        LOGGER.error(
                "Unhandled request failure correlationId={} exceptionType={}",
                CorrelationIds.currentOrCreate(),
                exception.getClass().getName());

        return response(
                HttpStatus.INTERNAL_SERVER_ERROR,
                "Server.UnexpectedError",
                "An unexpected error occurred.",
                request,
                List.of());
    }

    private ResponseEntity<ApiErrorResponse> response(
            HttpStatus status,
            String code,
            String message,
            HttpServletRequest request,
            List<ApiErrorResponse.Violation> violations) {
        ApiErrorResponse body = new ApiErrorResponse(
                Instant.now(clock),
                status.value(),
                code,
                message,
                CorrelationIds.currentOrCreate(),
                request.getRequestURI(),
                violations);
        return ResponseEntity.status(status).body(body);
    }
}

package com.agridrone.be1.identity.application.service.systemmanager;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.SystemManagerErrorCodes;
import com.agridrone.be1.identity.application.error.SystemManagerInvitationErrorCodes;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerInvitationUseCase;
import com.agridrone.be1.identity.application.port.out.config.SystemManagerInvitationPolicy;
import com.agridrone.be1.identity.application.port.out.notification.SystemManagerInvitationDelivery;
import com.agridrone.be1.identity.application.port.out.persistence.RoleRepository;
import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerInvitationRepository;
import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerProfileRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.InvitationTokenService;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.domain.SystemManagerInvitation;
import com.agridrone.be1.identity.domain.SystemManagerProfile;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.shared.audit.AuditEntry;
import com.agridrone.be1.shared.audit.AuditWriter;
import com.agridrone.be1.shared.error.StableApiException;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.fasterxml.jackson.databind.node.JsonNodeFactory;
import java.time.Clock;
import java.time.Instant;
import java.util.Locale;
import java.util.Objects;
import java.util.UUID;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.transaction.support.TransactionTemplate;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class SystemManagerInvitationService implements SystemManagerInvitationUseCase {
    private static final Logger LOGGER =
            LoggerFactory.getLogger(SystemManagerInvitationService.class);
    private static final String ROLE = "SYSTEM_MANAGER";

    private final UserRepository users;
    private final RoleRepository roles;
    private final SystemManagerProfileRepository profiles;
    private final SystemManagerInvitationRepository invitations;
    private final InvitationTokenService tokens;
    private final PasswordHasher passwordHasher;
    private final SystemManagerInvitationDelivery delivery;
    private final SystemManagerInvitationPolicy invitationPolicy;
    private final AuditWriter audit;
    private final Clock clock;
    private final TransactionTemplate transactions;

    public SystemManagerInvitationService(
            UserRepository users,
            RoleRepository roles,
            SystemManagerProfileRepository profiles,
            SystemManagerInvitationRepository invitations,
            InvitationTokenService tokens,
            PasswordHasher passwordHasher,
            ObjectProvider<SystemManagerInvitationDelivery> delivery,
            SystemManagerInvitationPolicy invitationPolicy,
            AuditWriter audit,
            Clock clock,
            TransactionTemplate transactions) {
        this.users = users;
        this.roles = roles;
        this.profiles = profiles;
        this.invitations = invitations;
        this.tokens = tokens;
        this.passwordHasher = passwordHasher;
        this.delivery = delivery.getIfAvailable();
        this.invitationPolicy = invitationPolicy;
        this.audit = audit;
        this.clock = clock;
        this.transactions = transactions;
    }

    @Override
    public InviteResult invite(String rawEmail) {
        InvitationCreation creation;
        try {
            creation = Objects.requireNonNull(transactions.execute(
                    status -> createInvitation(rawEmail)));
        } catch (DataIntegrityViolationException exception) {
            if (hasConstraint(exception, "uq_system_manager_invitations_pending_email")) {
                throw alreadyPending();
            }
            throw exception;
        }

        boolean sent = false;
        if (delivery != null) {
            try {
                delivery.send(
                        creation.result().email(),
                        creation.plainTextToken(),
                        creation.result().expiresAt());
                sent = true;
            } catch (RuntimeException exception) {
                LOGGER.error(
                        "System Manager invitation delivery failed invitationId={}",
                        creation.result().invitationId(),
                        exception);
            }
        }
        return new InviteResult(
                creation.result().invitationId(),
                creation.result().email(),
                creation.result().expiresAt(),
                sent);
    }

    private InvitationCreation createInvitation(String rawEmail) {
        UUID actorId = requireSystemAdmin();
        String email = rawEmail.trim().toLowerCase(Locale.ROOT);
        User existingUser = users.findByEmailIncludingDeleted(email).orElse(null);
        if (existingUser != null) {
            if (!existingUser.isActive()) {
                throw userInactive();
            }
            if (profiles.findByUserId(existingUser.id()).isPresent()) {
                throw alreadySystemManager();
            }
        }

        Instant now = clock.instant();
        SystemManagerInvitation pending = invitations.findPendingByEmail(email).orElse(null);
        if (pending != null) {
            if (pending.canAccept(now)) {
                throw alreadyPending();
            }
            invitations.save(pending.expire(now));
        }

        var generated = tokens.generate();
        SystemManagerInvitation invitation = SystemManagerInvitation.create(
                email,
                generated.tokenHash(),
                actorId,
                now.plus(invitationPolicy.expiration()),
                now);
        invitations.add(invitation);
        audit.append(new AuditEntry(
                null,
                "SystemManagerInvitation",
                invitation.id(),
                "CREATE",
                null,
                invitationSnapshot(invitation),
                null));
        return new InvitationCreation(
                new InviteResult(
                        invitation.id(), invitation.email(), invitation.expiresAt(), false),
                generated.plainTextToken());
    }

    @Override
    @Transactional(readOnly = true)
    public PreviewResult preview(String plainTextToken) {
        SystemManagerInvitation invitation = invitations
                .findByTokenHash(tokens.hash(plainTextToken.trim()))
                .filter(value -> value.canAccept(clock.instant()))
                .orElseThrow(SystemManagerInvitationService::invalidOrExpired);
        User user = users.findByEmailIncludingDeleted(invitation.email()).orElse(null);
        if (user != null) {
            if (!user.isActive()) {
                throw userInactive();
            }
            if (profiles.findByUserId(user.id()).isPresent()) {
                throw alreadySystemManager();
            }
        }
        return new PreviewResult(
                maskEmail(invitation.email()),
                ROLE,
                invitation.expiresAt(),
                user == null);
    }

    @Override
    public AcceptResult accept(AcceptCommand command) {
        try {
            return Objects.requireNonNull(transactions.execute(
                    status -> acceptInTransaction(command)));
        } catch (DataIntegrityViolationException exception) {
            if (hasConstraint(exception, "uq_system_manager_profiles_user")) {
                throw alreadySystemManager();
            }
            throw exception;
        }
    }

    private AcceptResult acceptInTransaction(AcceptCommand command) {
        Instant now = clock.instant();
        SystemManagerInvitation invitation = invitations
                .findByTokenHashForUpdate(tokens.hash(command.token().trim()))
                .filter(value -> value.canAccept(now))
                .orElseThrow(SystemManagerInvitationService::invalidOrExpired);
        User user = users.findByEmailIncludingDeleted(invitation.email()).orElse(null);
        boolean accountCreated = false;
        if (user == null) {
            if (command.fullName() == null
                    || command.fullName().isBlank()
                    || command.password() == null
                    || command.password().length() < 8) {
                throw registrationDetailsRequired();
            }
            user = User.create(
                    invitation.email(),
                    passwordHasher.hash(command.password()),
                    command.fullName().trim(),
                    normalizeOptional(command.phone()),
                    now);
            users.save(user);
            accountCreated = true;
        } else if (!user.isActive()) {
            throw userInactive();
        }

        if (profiles.findByUserId(user.id()).isPresent()) {
            throw alreadySystemManager();
        }
        var role = roles.findByCode(SystemRoleCodes.SYSTEM_MANAGER)
                .orElseThrow(SystemManagerInvitationService::roleMissing);
        if (!users.findSystemRoleCodes(user.id()).contains(SystemRoleCodes.SYSTEM_MANAGER)) {
            users.assignSystemRole(user.id(), role.id());
        }

        SystemManagerProfile profile = SystemManagerProfile.create(user.id(), now);
        profiles.save(profile);
        SystemManagerInvitation accepted = invitation.accept(user.id(), now);
        invitations.save(accepted);
        audit.append(new AuditEntry(
                null,
                "SystemManagerProfile",
                profile.id(),
                "CREATE_FROM_INVITATION",
                null,
                profileSnapshot(profile),
                null));
        audit.append(new AuditEntry(
                null,
                "SystemManagerInvitation",
                invitation.id(),
                "ACCEPT",
                invitationSnapshot(invitation),
                invitationSnapshot(accepted),
                null));
        return new AcceptResult(user.id(), profile.id(), accountCreated);
    }

    private UUID requireSystemAdmin() {
        var context = ExecutionContext.require();
        if (context.actorId() == null) {
            throw error(
                    HttpStatus.UNAUTHORIZED,
                    AuthenticationErrorCodes.CURRENT_USER_REQUIRED,
                    "A current user is required.");
        }
        if (!context.roles().contains(SystemRoleCodes.SYSTEM_ADMIN)) {
            throw error(
                    HttpStatus.FORBIDDEN,
                    "Auth.AccessDenied",
                    "System Administrator access is required.");
        }
        return context.actorId();
    }

    private static String maskEmail(String email) {
        int separator = email.indexOf('@');
        return separator <= 0 ? "***" : email.charAt(0) + "***" + email.substring(separator);
    }

    private static String normalizeOptional(String value) {
        return value == null || value.isBlank() ? null : value.trim();
    }

    private static com.fasterxml.jackson.databind.JsonNode invitationSnapshot(
            SystemManagerInvitation invitation) {
        return JsonNodeFactory.instance.objectNode()
                .put("email", invitation.email())
                .put("status", invitation.status().name())
                .put("expiresAt", invitation.expiresAt().toString());
    }

    private static com.fasterxml.jackson.databind.JsonNode profileSnapshot(
            SystemManagerProfile profile) {
        return JsonNodeFactory.instance.objectNode()
                .put("userId", profile.userId().toString())
                .put("status", profile.status().name())
                .put("availability", profile.availability().name())
                .put("qualificationStatus", profile.qualificationStatus().name())
                .put("version", profile.version());
    }

    private static StableApiException alreadyPending() {
        return error(
                HttpStatus.CONFLICT,
                SystemManagerInvitationErrorCodes.ALREADY_PENDING,
                "An active System Manager invitation already exists for this email.");
    }

    private static StableApiException invalidOrExpired() {
        return error(
                HttpStatus.UNPROCESSABLE_ENTITY,
                SystemManagerInvitationErrorCodes.INVALID_OR_EXPIRED,
                "The invitation is invalid, expired, or has already been used.");
    }

    private static StableApiException registrationDetailsRequired() {
        return error(
                HttpStatus.UNPROCESSABLE_ENTITY,
                SystemManagerInvitationErrorCodes.REGISTRATION_DETAILS_REQUIRED,
                "Full name and a password of at least 8 characters are required.");
    }

    private static StableApiException alreadySystemManager() {
        return error(
                HttpStatus.CONFLICT,
                SystemManagerInvitationErrorCodes.ALREADY_SYSTEM_MANAGER,
                "The user is already registered as a System Manager.");
    }

    private static StableApiException userInactive() {
        return error(
                HttpStatus.CONFLICT,
                SystemManagerInvitationErrorCodes.USER_INACTIVE,
                "The invited user account is inactive.");
    }

    private static StableApiException roleMissing() {
        return error(
                HttpStatus.INTERNAL_SERVER_ERROR,
                SystemManagerErrorCodes.ROLE_MISSING,
                "The SYSTEM_MANAGER role has not been seeded.");
    }

    private static StableApiException error(HttpStatus status, String code, String message) {
        return new StableApiException(status, code, message);
    }

    private static boolean hasConstraint(Throwable exception, String constraint) {
        String expected = constraint.toLowerCase(Locale.ROOT);
        Throwable current = exception;
        while (current != null) {
            if (current.getMessage() != null
                    && current.getMessage().toLowerCase(Locale.ROOT).contains(expected)) {
                return true;
            }
            current = current.getCause();
        }
        return false;
    }

    private record InvitationCreation(InviteResult result, String plainTextToken) {}
}

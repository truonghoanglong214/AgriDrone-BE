package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.Locale;
import java.util.UUID;

public final class User {
    private final UUID id;
    private final String email;
    private String passwordHash;
    private String fullName;
    private String phone;
    private UserStatus status;
    private Instant lastLoginAt;
    private final Instant createdAt;
    private Instant updatedAt;
    private final Instant deletedAt;

    public User(
            UUID id,
            String email,
            String passwordHash,
            String fullName,
            String phone,
            UserStatus status,
            Instant lastLoginAt,
            Instant createdAt,
            Instant updatedAt,
            Instant deletedAt) {
        this.id = requireId(id);
        this.email = normalizeEmail(email);
        this.passwordHash = requireText(passwordHash, "passwordHash");
        this.fullName = requireText(fullName, "fullName");
        this.phone = normalizeOptional(phone);
        this.status = java.util.Objects.requireNonNull(status);
        this.lastLoginAt = lastLoginAt;
        this.createdAt = java.util.Objects.requireNonNull(createdAt);
        this.updatedAt = java.util.Objects.requireNonNull(updatedAt);
        this.deletedAt = deletedAt;
    }

    public static User create(
            String email,
            String passwordHash,
            String fullName,
            String phone,
            Instant now) {
        return new User(
                UUID.randomUUID(),
                email,
                passwordHash,
                fullName,
                phone,
                UserStatus.ACTIVE,
                null,
                now,
                now,
                null);
    }

    public void updateProfile(String fullName, String phone, Instant now) {
        this.fullName = requireText(fullName, "fullName");
        this.phone = normalizeOptional(phone);
        this.updatedAt = java.util.Objects.requireNonNull(now);
    }

    public void changePassword(String passwordHash, Instant now) {
        this.passwordHash = requireText(passwordHash, "passwordHash");
        this.updatedAt = java.util.Objects.requireNonNull(now);
    }

    public void recordLogin(Instant now) {
        this.lastLoginAt = now;
        this.updatedAt = now;
    }

    public boolean isActive() {
        return status == UserStatus.ACTIVE && deletedAt == null;
    }

    public UUID id() {
        return id;
    }

    public String email() {
        return email;
    }

    public String passwordHash() {
        return passwordHash;
    }

    public String fullName() {
        return fullName;
    }

    public String phone() {
        return phone;
    }

    public UserStatus status() {
        return status;
    }

    public Instant lastLoginAt() {
        return lastLoginAt;
    }

    public Instant createdAt() {
        return createdAt;
    }

    public Instant updatedAt() {
        return updatedAt;
    }

    public Instant deletedAt() {
        return deletedAt;
    }

    private static UUID requireId(UUID value) {
        return java.util.Objects.requireNonNull(value, "id");
    }

    private static String normalizeEmail(String value) {
        return requireText(value, "email").toLowerCase(Locale.ROOT);
    }

    private static String requireText(String value, String name) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(name + " is required");
        }
        return value.trim();
    }

    private static String normalizeOptional(String value) {
        return value == null || value.isBlank() ? null : value.trim();
    }
}

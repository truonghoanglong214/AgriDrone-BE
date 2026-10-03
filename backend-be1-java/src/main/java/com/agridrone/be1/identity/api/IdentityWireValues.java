package com.agridrone.be1.identity.api;

import com.agridrone.be1.identity.domain.TenantStatus;
import com.agridrone.be1.identity.domain.ManagerAvailability;
import com.agridrone.be1.identity.domain.QualificationStatus;
import com.agridrone.be1.identity.domain.SystemManagerProfileStatus;

public final class IdentityWireValues {
    private IdentityWireValues() {
    }

    public static int tenantStatus(TenantStatus status) {
        return switch (status) {
            case ACTIVE -> 0;
            case INACTIVE -> 1;
        };
    }

    public static int role(String role) {
        if ("OWNER".equals(role)) {
            return 0;
        }
        throw new IllegalArgumentException("Unsupported tenant role: " + role);
    }

    public static int membershipStatus(String status) {
        return switch (status) {
            case "ACTIVE" -> 0;
            case "INACTIVE" -> 1;
            default -> throw new IllegalArgumentException(
                    "Unsupported membership status: " + status);
        };
    }

    public static int systemManagerProfileStatus(SystemManagerProfileStatus status) {
        return switch (status) {
            case ACTIVE -> 0;
            case SUSPENDED -> 1;
        };
    }

    public static int managerAvailability(ManagerAvailability availability) {
        return switch (availability) {
            case AVAILABLE -> 0;
            case UNAVAILABLE -> 1;
        };
    }

    public static ManagerAvailability managerAvailability(int value) {
        return switch (value) {
            case 0 -> ManagerAvailability.AVAILABLE;
            case 1 -> ManagerAvailability.UNAVAILABLE;
            default -> throw new IllegalArgumentException(
                    "Unsupported SystemManager availability: " + value);
        };
    }

    public static int qualificationStatus(QualificationStatus status) {
        return switch (status) {
            case PENDING -> 0;
            case QUALIFIED -> 1;
            case SUSPENDED -> 2;
            case REVOKED -> 3;
        };
    }

    public static QualificationStatus qualificationStatus(int value) {
        return switch (value) {
            case 0 -> QualificationStatus.PENDING;
            case 1 -> QualificationStatus.QUALIFIED;
            case 2 -> QualificationStatus.SUSPENDED;
            case 3 -> QualificationStatus.REVOKED;
            default -> throw new IllegalArgumentException(
                    "Unsupported SystemManager qualification: " + value);
        };
    }
}

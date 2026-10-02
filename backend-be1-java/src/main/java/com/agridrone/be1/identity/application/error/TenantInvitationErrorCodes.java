package com.agridrone.be1.identity.application.error;

public final class TenantInvitationErrorCodes {
    public static final String INVITE_SELF_NOT_ALLOWED =
            "TenantInvitation.InviteSelfNotAllowed";
    public static final String USER_ALREADY_MEMBER =
            "TenantInvitation.UserAlreadyMember";
    public static final String ALREADY_PENDING = "TenantInvitation.AlreadyPending";
    public static final String OWNER_ALREADY_ASSIGNED =
            "TenantInvitation.OwnerAlreadyAssigned";
    public static final String OWNER_PROVISIONING_ALREADY_PENDING =
            "TenantInvitation.OwnerProvisioningAlreadyPending";
    public static final String INVALID_OR_EXPIRED =
            "TenantInvitation.InvalidOrExpired";
    public static final String REGISTRATION_DETAILS_REQUIRED =
            "TenantInvitation.RegistrationDetailsRequired";
    public static final String USER_INACTIVE = "TenantInvitation.UserInactive";

    private TenantInvitationErrorCodes() {
    }
}

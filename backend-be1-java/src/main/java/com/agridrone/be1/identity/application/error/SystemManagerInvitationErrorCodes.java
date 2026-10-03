package com.agridrone.be1.identity.application.error;

public final class SystemManagerInvitationErrorCodes {
    public static final String ALREADY_PENDING = "SystemManagerInvitation.AlreadyPending";
    public static final String INVALID_OR_EXPIRED =
            "SystemManagerInvitation.InvalidOrExpired";
    public static final String REGISTRATION_DETAILS_REQUIRED =
            "SystemManagerInvitation.RegistrationDetailsRequired";
    public static final String ALREADY_SYSTEM_MANAGER =
            "SystemManagerInvitation.AlreadySystemManager";
    public static final String USER_INACTIVE = "SystemManagerInvitation.UserInactive";

    private SystemManagerInvitationErrorCodes() {}
}

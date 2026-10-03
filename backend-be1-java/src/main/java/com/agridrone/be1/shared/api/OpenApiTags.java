package com.agridrone.be1.shared.api;

public final class OpenApiTags {
    public static final String AUTHENTICATION = "Authentication";
    public static final String AUTHENTICATION_DESCRIPTION =
            "Login, tenant selection, JWT metadata, and password recovery.";

    public static final String INVITATIONS = "Invitations";
    public static final String INVITATIONS_DESCRIPTION =
            "Tenant and system-manager invitation issuance, preview, and acceptance.";

    public static final String TENANT_ADMINISTRATION = "Tenant Administration";
    public static final String TENANT_ADMINISTRATION_DESCRIPTION =
            "System-admin tenant lifecycle and tenant membership queries.";

    public static final String SYSTEM_MANAGER_ADMINISTRATION =
            "System Manager Administration";
    public static final String SYSTEM_MANAGER_ADMINISTRATION_DESCRIPTION =
            "System-admin manager profiles, qualification, availability, and farm assignments.";

    public static final String SYSTEM_MANAGER_WORKSPACE = "System Manager Workspace";
    public static final String SYSTEM_MANAGER_WORKSPACE_DESCRIPTION =
            "System-manager access to assigned farms and operational work.";

    public static final String MESSAGING_OPERATIONS = "Messaging Operations";
    public static final String MESSAGING_OPERATIONS_DESCRIPTION =
            "System-admin outbox and dead-letter recovery operations.";

    private OpenApiTags() {
    }
}

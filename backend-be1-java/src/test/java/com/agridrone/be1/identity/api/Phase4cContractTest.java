package com.agridrone.be1.identity.api;

import static org.assertj.core.api.Assertions.assertThat;

import com.agridrone.be1.identity.api.auth.login.LoginResponse;
import com.agridrone.be1.identity.api.auth.selection.SelectTenantRequest;
import com.agridrone.be1.identity.api.tenant.CreateTenantRequest;
import com.agridrone.be1.identity.api.tenant.CreateTenantResponse;
import com.agridrone.be1.identity.api.tenant.ProvisionTenantOwnerRequest;
import com.agridrone.be1.identity.api.tenant.ProvisionTenantOwnerResponse;
import com.agridrone.be1.identity.api.tenant.TenantListItemResponse;
import com.agridrone.be1.identity.api.tenant.UserTenantListItemResponse;
import com.agridrone.be1.identity.api.tenantinvitation.AcceptTenantInvitationRequest;
import com.agridrone.be1.identity.api.tenantinvitation.AcceptTenantInvitationResponse;
import com.agridrone.be1.identity.api.tenantinvitation.PreviewTenantInvitationRequest;
import com.agridrone.be1.identity.api.tenantinvitation.PreviewTenantInvitationResponse;
import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.error.TenantInvitationErrorCodes;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.validation.Validation;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class Phase4cContractTest {
    private static final UUID FIRST_ID =
            UUID.fromString("11111111-1111-1111-1111-111111111111");
    private static final UUID SECOND_ID =
            UUID.fromString("22222222-2222-2222-2222-222222222222");
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");

    private final ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();

    @Test
    void freezesPhase4cRequestAndResponseFieldNames() {
        assertFields(new SelectTenantRequest("selection-token", FIRST_ID),
                "selectionToken", "tenantId");
        assertFields(new CreateTenantRequest("TENANT", "Tenant"),
                "tenantCode", "tenantName");
        assertFields(new ProvisionTenantOwnerRequest("owner@example.com"), "email");
        assertFields(new PreviewTenantInvitationRequest("invitation-token"), "token");
        assertFields(new AcceptTenantInvitationRequest(
                        "invitation-token", "Password123!", "Owner", null),
                "token", "password", "fullName", "phone");

        assertFields(new CreateTenantResponse(
                        FIRST_ID, "TENANT", "Tenant", 0, NOW),
                "tenantId", "code", "name", "status", "createdAt");
        assertFields(new ProvisionTenantOwnerResponse(
                        FIRST_ID, "owner@example.com", NOW),
                "invitationId", "email", "expiresAt");
        assertFields(new PreviewTenantInvitationResponse(
                        "o***@example.com", "Tenant", 0, NOW, true),
                "maskedEmail", "tenantName", "role", "expiresAt",
                "requiresAccountCreation");
        assertFields(new AcceptTenantInvitationResponse(
                        FIRST_ID, SECOND_ID, 0, true),
                "userId", "tenantId", "role", "accountCreated");
        assertFields(new TenantListItemResponse(
                        FIRST_ID, "TENANT", "Tenant", 0, NOW),
                "id", "code", "name", "status", "createdAt");
        assertFields(new UserTenantListItemResponse(
                        FIRST_ID, SECOND_ID, 0, 0, NOW, NOW),
                "id", "tenantId", "role", "status", "joinedAt", "createdAt");
    }

    @Test
    void freezesLoginSelectionAndDotnetPaginationShape() {
        var tenant = new LoginResponse.Tenant(FIRST_ID, "TENANT", "Tenant", 0);
        var response = new LoginResponse(
                "owner@example.com",
                "Owner",
                null,
                null,
                new LoginResponse.TenantSelection("selection-token", NOW, List.of(tenant)));
        assertFields(response, "email", "fullName", "phone", "session", "tenantSelection");

        var page = PageResponse.of(
                List.of(new TenantListItemResponse(
                        FIRST_ID, "TENANT", "Tenant", 0, NOW)),
                new PageRequest(2, 20),
                41);
        assertThat(mapper.valueToTree(page).fieldNames())
                .toIterable()
                .containsExactly(
                        "items", "pageNumber", "pageSize", "totalCount",
                        "totalPages", "hasPreviousPage", "hasNextPage");
        assertThat(page.totalPages()).isEqualTo(3);
        assertThat(page.hasPreviousPage()).isTrue();
        assertThat(page.hasNextPage()).isTrue();
        assertThat(PageRequest.defaults())
                .isEqualTo(new PageRequest(1, 20));
    }

    @Test
    void freezesStableErrorCodes() {
        assertThat(AuthenticationErrorCodes.INVALID_TENANT_SELECTION_TOKEN)
                .isEqualTo("Authentication.InvalidTenantSelectionToken");
        assertThat(TenantErrorCodes.ACCESS_DENIED).isEqualTo("Tenant.AccessDenied");
        assertThat(TenantErrorCodes.NOT_FOUND).isEqualTo("Tenant.NotFound");
        assertThat(TenantErrorCodes.INACTIVE).isEqualTo("Tenant.Inactive");
        assertThat(TenantInvitationErrorCodes.INVALID_OR_EXPIRED)
                .isEqualTo("TenantInvitation.InvalidOrExpired");
        assertThat(TenantInvitationErrorCodes.OWNER_ALREADY_ASSIGNED)
                .isEqualTo("TenantInvitation.OwnerAlreadyAssigned");
        assertThat(TenantInvitationErrorCodes.OWNER_PROVISIONING_ALREADY_PENDING)
                .isEqualTo("TenantInvitation.OwnerProvisioningAlreadyPending");
        assertThat(TenantInvitationErrorCodes.USER_ALREADY_MEMBER)
                .isEqualTo("TenantInvitation.UserAlreadyMember");
        assertThat(TenantInvitationErrorCodes.REGISTRATION_DETAILS_REQUIRED)
                .isEqualTo("TenantInvitation.RegistrationDetailsRequired");
        assertThat(TenantInvitationErrorCodes.USER_INACTIVE)
                .isEqualTo("TenantInvitation.UserInactive");
    }

    @Test
    void requestValidationRejectsMissingCredentialsAndInvalidPaging() {
        try (var validatorFactory = Validation.buildDefaultValidatorFactory()) {
            var validator = validatorFactory.getValidator();
            assertThat(validator.validate(new SelectTenantRequest("", null))).hasSize(2);
            assertThat(validator.validate(new ProvisionTenantOwnerRequest("not-an-email")))
                    .isNotEmpty();
            assertThat(validator.validate(new AcceptTenantInvitationRequest(
                            "", null, null, null)))
                    .isNotEmpty();
            assertThat(validator.validate(new AcceptTenantInvitationRequest(
                            "token", "short", "   ", null)))
                    .hasSize(2);
        }

        org.assertj.core.api.Assertions.assertThatThrownBy(() -> new PageRequest(0, 20))
                .isInstanceOf(IllegalArgumentException.class);
        org.assertj.core.api.Assertions.assertThatThrownBy(() -> new PageRequest(1, 101))
                .isInstanceOf(IllegalArgumentException.class);
    }

    private void assertFields(Object value, String... expectedFields) {
        assertThat(mapper.valueToTree(value).fieldNames())
                .toIterable()
                .containsExactly(expectedFields);
    }
}

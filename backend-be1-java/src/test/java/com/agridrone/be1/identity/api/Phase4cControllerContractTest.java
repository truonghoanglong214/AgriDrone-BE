package com.agridrone.be1.identity.api;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.doNothing;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.agridrone.be1.identity.api.auth.selection.SelectTenantController;
import com.agridrone.be1.identity.api.tenant.SystemTenantsController;
import com.agridrone.be1.identity.api.tenant.SystemUserTenantsController;
import com.agridrone.be1.identity.api.tenantinvitation.TenantInvitationController;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserResult;
import com.agridrone.be1.identity.application.port.in.selecttenant.SelectTenantUseCase;
import com.agridrone.be1.identity.application.port.in.tenantadmin.TenantAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantinvitation.TenantInvitationUseCase;
import com.agridrone.be1.identity.application.port.in.tenantquery.TenantQueryUseCase;
import com.agridrone.be1.identity.application.readmodel.TenantListItem;
import com.agridrone.be1.identity.application.readmodel.UserTenantListItem;
import com.agridrone.be1.identity.domain.TenantStatus;
import com.agridrone.be1.shared.api.PageResponse;
import com.agridrone.be1.shared.error.GlobalApiExceptionHandler;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.http.MediaType;
import org.springframework.http.converter.json.MappingJackson2HttpMessageConverter;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

class Phase4cControllerContractTest {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");
    private final UUID tenantId = UUID.randomUUID();
    private final UUID userId = UUID.randomUUID();
    private SelectTenantUseCase selectTenant;
    private TenantAdministrationUseCase administration;
    private TenantInvitationUseCase invitations;
    private TenantQueryUseCase queries;
    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        selectTenant = org.mockito.Mockito.mock(SelectTenantUseCase.class);
        administration = org.mockito.Mockito.mock(TenantAdministrationUseCase.class);
        invitations = org.mockito.Mockito.mock(TenantInvitationUseCase.class);
        queries = org.mockito.Mockito.mock(TenantQueryUseCase.class);
        ObjectMapper mapper = new ObjectMapper()
                .findAndRegisterModules()
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        mockMvc = MockMvcBuilders.standaloneSetup(
                        new SelectTenantController(selectTenant),
                        new SystemTenantsController(administration, invitations, queries),
                        new SystemUserTenantsController(queries),
                        new TenantInvitationController(invitations))
                .setMessageConverters(new MappingJackson2HttpMessageConverter(mapper))
                .setControllerAdvice(new GlobalApiExceptionHandler(
                        Clock.fixed(NOW, ZoneOffset.UTC)))
                .build();
    }

    @Test
    void selectTenantReturnsNormalLoginSessionShape() throws Exception {
        when(selectTenant.select(any())).thenReturn(new LoginUserResult(
                "owner@example.com",
                "Owner",
                null,
                new LoginUserResult.Session(
                        "access-token",
                        NOW.plusSeconds(900),
                        new LoginUserResult.Tenant(
                                tenantId, "FARM", "Farm", "OWNER")),
                null));

        mockMvc.perform(post("/api/auth/select-tenant")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"selectionToken\":\"selection-token\",\"tenantId\":\""
                                + tenantId + "\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.session.accessToken").value("access-token"))
                .andExpect(jsonPath("$.session.tenant.id").value(tenantId.toString()))
                .andExpect(jsonPath("$.session.tenant.role").value(0))
                .andExpect(jsonPath("$.tenantSelection").doesNotExist());
    }

    @Test
    void tenantLifecycleRoutesPreserveStatusAndResponseShapes() throws Exception {
        when(administration.create(any())).thenReturn(
                new TenantAdministrationUseCase.CreateTenantResult(
                        tenantId, "FARM", "Farm", TenantStatus.ACTIVE, NOW));
        doNothing().when(administration).activate(tenantId);
        doNothing().when(administration).deactivate(tenantId);

        mockMvc.perform(post("/api/system/tenants")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"tenantCode\":\"farm\",\"tenantName\":\"Farm\"}"))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.tenantId").value(tenantId.toString()))
                .andExpect(jsonPath("$.status").value(0));
        mockMvc.perform(put("/api/system/tenants/{tenantId}/activate", tenantId))
                .andExpect(status().isNoContent());
        mockMvc.perform(put("/api/system/tenants/{tenantId}/deactivate", tenantId))
                .andExpect(status().isNoContent());
    }

    @Test
    void ownerProvisionPreviewAndAbsoluteAcceptRoutesMatchFrozenContract() throws Exception {
        UUID invitationId = UUID.randomUUID();
        when(invitations.provisionOwner(eq(tenantId), anyString())).thenReturn(
                new TenantInvitationUseCase.ProvisionResult(
                        invitationId, "owner@example.com", NOW.plusSeconds(3600)));
        when(invitations.preview("plain-token")).thenReturn(
                new TenantInvitationUseCase.PreviewResult(
                        "o***@example.com", "Farm", "OWNER",
                        NOW.plusSeconds(3600), true));
        when(invitations.accept(any())).thenReturn(
                new TenantInvitationUseCase.AcceptResult(
                        userId, tenantId, "OWNER", true));

        mockMvc.perform(post("/api/system/tenants/{tenantId}/owner-provisionings", tenantId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"email\":\"owner@example.com\"}"))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.invitationId").value(invitationId.toString()));
        mockMvc.perform(post("/api/auth/invitations/preview")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"token\":\"plain-token\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.maskedEmail").value("o***@example.com"))
                .andExpect(jsonPath("$.role").value(0))
                .andExpect(jsonPath("$.requiresAccountCreation").value(true));
        mockMvc.perform(post("/invitations/accept")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"token\":\"plain-token\",\"password\":\"Password123!\","
                                + "\"fullName\":\"Owner\",\"phone\":null}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.userId").value(userId.toString()))
                .andExpect(jsonPath("$.tenantId").value(tenantId.toString()))
                .andExpect(jsonPath("$.role").value(0))
                .andExpect(jsonPath("$.accountCreated").value(true));
    }

    @Test
    void systemReadModelsUseExactOneBasedPagedEnvelope() throws Exception {
        when(queries.findTenants(any())).thenReturn(new PageResponse<>(
                List.of(new TenantListItem(
                        tenantId, "FARM", "Farm", TenantStatus.INACTIVE, NOW)),
                2, 5, 6, 2, true, false));
        UUID membershipId = UUID.randomUUID();
        when(queries.findUserTenants(eq(userId), any())).thenReturn(new PageResponse<>(
                List.of(new UserTenantListItem(
                        membershipId, tenantId, "OWNER", "ACTIVE", NOW, NOW)),
                1, 20, 1, 1, false, false));

        mockMvc.perform(get("/api/system/tenants/all")
                        .param("pageNumber", "2")
                        .param("pageSize", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.items[0].status").value(1))
                .andExpect(jsonPath("$.pageNumber").value(2))
                .andExpect(jsonPath("$.totalCount").value(6))
                .andExpect(jsonPath("$.hasPreviousPage").value(true));
        mockMvc.perform(get("/api/system/users/{userId}/tenants", userId))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.items[0].role").value(0))
                .andExpect(jsonPath("$.items[0].status").value(0))
                .andExpect(jsonPath("$.pageSize").value(20));
    }
}

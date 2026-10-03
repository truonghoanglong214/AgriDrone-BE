package com.agridrone.be1.identity.api;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.doNothing;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.agridrone.be1.identity.api.systemmanager.SystemFarmManagersController;
import com.agridrone.be1.identity.api.systemmanager.SystemManagerWorkController;
import com.agridrone.be1.identity.api.systemmanager.SystemManagersController;
import com.agridrone.be1.identity.api.systemmanagerinvitation.SystemManagerInvitationController;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerAdministrationUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerInvitationUseCase;
import com.agridrone.be1.identity.application.port.in.systemmanager.SystemManagerWorkUseCase;
import com.agridrone.be1.identity.domain.ManagerAvailability;
import com.agridrone.be1.identity.domain.QualificationStatus;
import com.agridrone.be1.identity.domain.SystemManagerProfileStatus;
import com.agridrone.be1.shared.error.GlobalApiExceptionHandler;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import java.math.BigDecimal;
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

class Phase4dControllerContractTest {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");
    private final UUID userId = UUID.randomUUID();
    private final UUID profileId = UUID.randomUUID();
    private final UUID farmId = UUID.randomUUID();
    private final UUID tenantId = UUID.randomUUID();
    private SystemManagerAdministrationUseCase administration;
    private SystemManagerInvitationUseCase invitations;
    private SystemManagerWorkUseCase work;
    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        administration = org.mockito.Mockito.mock(SystemManagerAdministrationUseCase.class);
        invitations = org.mockito.Mockito.mock(SystemManagerInvitationUseCase.class);
        work = org.mockito.Mockito.mock(SystemManagerWorkUseCase.class);
        ObjectMapper mapper = new ObjectMapper()
                .findAndRegisterModules()
                .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS);
        mockMvc = MockMvcBuilders.standaloneSetup(
                        new SystemManagersController(administration, invitations),
                        new SystemFarmManagersController(administration),
                        new SystemManagerWorkController(work),
                        new SystemManagerInvitationController(invitations))
                .setMessageConverters(new MappingJackson2HttpMessageConverter(mapper))
                .setControllerAdvice(new GlobalApiExceptionHandler(
                        Clock.fixed(NOW, ZoneOffset.UTC)))
                .build();
    }

    @Test
    void profileLifecycleUsesFrozenNumericEnumWireValues() throws Exception {
        var result = new SystemManagerAdministrationUseCase.ProfileResult(
                profileId,
                userId,
                "manager@example.com",
                "Manager",
                SystemManagerProfileStatus.ACTIVE,
                ManagerAvailability.AVAILABLE,
                QualificationStatus.QUALIFIED,
                NOW.plusSeconds(86400),
                4);
        when(administration.createProfile(userId)).thenReturn(result);
        when(administration.activate(eq(profileId), anyString(), anyLong())).thenReturn(result);
        when(administration.suspend(eq(profileId), anyString(), anyLong())).thenReturn(result);
        when(administration.updateAvailability(
                eq(profileId), eq(ManagerAvailability.AVAILABLE), anyString(), anyLong()))
                .thenReturn(result);
        when(administration.updateQualification(
                eq(profileId), eq(QualificationStatus.QUALIFIED), any(), anyString(), anyLong()))
                .thenReturn(result);

        mockMvc.perform(post("/api/system/managers")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"userId\":\"" + userId + "\"}"))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.status").value(0))
                .andExpect(jsonPath("$.availability").value(0))
                .andExpect(jsonPath("$.qualificationStatus").value(1));
        mockMvc.perform(put("/api/system/managers/{profileId}/activate", profileId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"approved\",\"expectedVersion\":3}"))
                .andExpect(status().isOk());
        mockMvc.perform(put("/api/system/managers/{profileId}/suspend", profileId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"paused\",\"expectedVersion\":3}"))
                .andExpect(status().isOk());
        mockMvc.perform(put("/api/system/managers/{profileId}/availability", profileId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"availability\":0,\"reason\":\"ready\","
                                + "\"expectedVersion\":3}"))
                .andExpect(status().isOk());
        mockMvc.perform(put("/api/system/managers/{profileId}/qualification", profileId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"status\":1,\"expiresAt\":\"2026-10-03T00:00:00Z\","
                                + "\"reason\":\"verified\",\"expectedVersion\":3}"))
                .andExpect(status().isOk());
    }

    @Test
    void invitationRoutesPreservePreviewAndAcceptanceShapes() throws Exception {
        UUID invitationId = UUID.randomUUID();
        when(invitations.invite("manager@example.com")).thenReturn(
                new SystemManagerInvitationUseCase.InviteResult(
                        invitationId, "manager@example.com", NOW.plusSeconds(3600), false));
        when(invitations.preview("plain-token")).thenReturn(
                new SystemManagerInvitationUseCase.PreviewResult(
                        "m***@example.com", "SYSTEM_MANAGER", NOW.plusSeconds(3600), true));
        when(invitations.accept(any())).thenReturn(
                new SystemManagerInvitationUseCase.AcceptResult(userId, profileId, true));

        mockMvc.perform(post("/api/system/managers/invitations")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"email\":\"manager@example.com\"}"))
                .andExpect(status().isCreated())
                .andExpect(jsonPath("$.invitationId").value(invitationId.toString()))
                .andExpect(jsonPath("$.emailSent").value(false));
        mockMvc.perform(post("/api/auth/system-manager-invitations/preview")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"token\":\"plain-token\"}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.role").value("SYSTEM_MANAGER"))
                .andExpect(jsonPath("$.requiresAccountCreation").value(true));
        mockMvc.perform(post("/api/auth/system-manager-invitations/accept")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"token\":\"plain-token\",\"password\":\"Password123!\","
                                + "\"fullName\":\"Manager\",\"phone\":null}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.userId").value(userId.toString()))
                .andExpect(jsonPath("$.profileId").value(profileId.toString()))
                .andExpect(jsonPath("$.accountCreated").value(true));
    }

    @Test
    void assignmentAndAssignedFarmRoutesMatchCurrentContract() throws Exception {
        UUID assignmentId = UUID.randomUUID();
        when(administration.assignPrimary(eq(farmId), eq(profileId), anyString(), eq(null)))
                .thenReturn(new SystemManagerAdministrationUseCase.AssignmentResult(
                        assignmentId, tenantId, farmId, profileId, userId, NOW, 1));
        doNothing().when(administration).endPrimary(eq(farmId), anyString(), eq(1L));
        when(work.findAssignedFarms()).thenReturn(List.of(
                new SystemManagerWorkUseCase.AssignedFarmResult(
                        tenantId, farmId, "FARM", "Farm", "Address",
                        new BigDecimal("12.5000"), NOW)));

        mockMvc.perform(put("/api/system/farms/{farmId}/primary-manager", farmId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"systemManagerProfileId\":\"" + profileId + "\","
                                + "\"reason\":\"assigned\","
                                + "\"expectedCurrentAssignmentVersion\":null}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.assignmentId").value(assignmentId.toString()))
                .andExpect(jsonPath("$.version").value(1));
        mockMvc.perform(get("/api/system-manager/farms"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[0].farmId").value(farmId.toString()))
                .andExpect(jsonPath("$[0].areaHectares").value(12.5));
        mockMvc.perform(put("/api/system/farms/{farmId}/primary-manager/end", farmId)
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("{\"reason\":\"ended\",\"expectedVersion\":1}"))
                .andExpect(status().isNoContent());
    }
}

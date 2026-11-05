import html

def escape(s):
    return html.escape(s).replace('\n', '&#xa;')

def generate_class_diagram_drawio():
    xml = [
        "<?xml version='1.0' encoding='utf-8'?>",
        "<mxfile host=\"app.diagrams.net\" version=\"24.0.0\">",
        "  <diagram id=\"chinh_class_dia\" name=\"Figure 3.4.1 Class Diagram - Plant Health Analysis &amp; Publication\">",
        "    <mxGraphModel dx=\"1600\" dy=\"1200\" grid=\"1\" gridSize=\"10\" guides=\"1\" tooltips=\"1\" connect=\"1\" arrows=\"1\" fold=\"1\" page=\"1\" pageScale=\"1\" pageWidth=\"1800\" pageHeight=\"1450\" math=\"0\" shadow=\"0\">",
        "      <root>",
        "        <mxCell id=\"0\" />",
        "        <mxCell id=\"1\" parent=\"0\" />"
    ]

    # Title
    title_text = "<b>Figure 3.4.1: Class Diagram — Plant Health Analysis &amp; Publication Package</b><br/><font style='font-size:11px; font-weight:normal; color:#555555;'>Detailed Design Model · AgriDrone BE Service (ASP.NET Core 8 / MediatR / Clean Architecture)</font>"
    xml.append(f'        <mxCell id="title" parent="1" value="{title_text}" style="text;html=1;align=center;verticalAlign=middle;whiteSpace=wrap;rounded=1;fillColor=#F8F9FA;strokeColor=#D0D7DE;fontSize=14;fontColor=#24292F;" vertex="1">')
    xml.append('          <mxGeometry x="60" y="20" width="1580" height="45" as="geometry" />')
    xml.append('        </mxCell>')

    def add_class(id, name, stereotype, fields, methods, x, y, w, h, fill="#FFFFFF", stroke="#000000"):
        content = []
        if stereotype:
            content.append(f"&lt;&lt;{stereotype}&gt;&gt;")
        content.append(f"<b>{name}</b>")
        header = "<br/>".join(content)
        
        body_parts = [f"<p style='margin:0px;padding:4px;text-align:center;'>{header}</p><hr size='1'/>"]
        if fields:
            fields_str = "<br/>".join([f"+ {f}" if not f.startswith(('-', '+', '#')) else f for f in fields])
            body_parts.append(f"<p style='margin:0px;padding:4px;text-align:left;font-size:10px;'>{fields_str}</p>")
            body_parts.append("<hr size='1'/>")
        else:
            body_parts.append("<hr size='1'/>")
            
        if methods:
            methods_str = "<br/>".join([f"+ {m}" if not m.startswith(('-', '+', '#')) else m for m in methods])
            body_parts.append(f"<p style='margin:0px;padding:4px;text-align:left;font-size:10px;'>{methods_str}</p>")
            
        full_val = "".join(body_parts)
        xml.append(f'        <mxCell id="{id}" parent="1" value="{full_val}" style="swimlane;fontStyle=0;childLayout=stackLayout;horizontal=1;startSize=26;fillColor={fill};strokeColor={stroke};rounded=0;shadow=0;comic=0;labelBackgroundColor=none;strokeWidth=1;fontSize=11;align=center;" vertex="1">')
        xml.append(f'          <mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry" />')
        xml.append('        </mxCell>')

    # 1. CONTROLLERS
    c1_methods = [
        "+ TriggerDiseaseDetection(farmId: Guid, missionId: Guid): Task&lt;IActionResult&gt;",
        "+ GetPlantHealthMap(farmId: Guid, tenantId: Guid?): Task&lt;IActionResult&gt;",
        "+ ReviewSuspectedDisease(farmId: Guid, detectionId: Guid, req: ReviewRequest): Task&lt;IActionResult&gt;",
        "+ GetFlaggedLocations(farmId: Guid, missionId: Guid): Task&lt;IActionResult&gt;",
        "+ ResolveFlaggedLocation(farmId: Guid, detectionId: Guid, req: ResolveFlagRequest): Task&lt;IActionResult&gt;"
    ]
    add_class("ctrl_plant_health", "FarmPlantHealthController", "controller", ["- _sender: ISender"], c1_methods, 60, 90, 480, 160)

    c2_methods = [
        "+ GetDiseaseZones(farmId: Guid, surveyResultId: Guid?): Task&lt;IActionResult&gt;",
        "+ MergeDiseaseZones(farmId: Guid, req: MergeDiseaseZonesRequest): Task&lt;IActionResult&gt;",
        "+ UpdateZoneBoundary(farmId: Guid, zoneId: Guid, req: UpdateBoundaryRequest): Task&lt;IActionResult&gt;",
        "+ UpdateZonePlantMemberships(farmId: Guid, zoneId: Guid, req: UpdateMembershipsRequest): Task&lt;IActionResult&gt;",
        "+ GetTreatmentGuidance(farmId: Guid, zoneId: Guid): Task&lt;IActionResult&gt;",
        "+ AttachCustomTreatmentGuidance(farmId: Guid, zoneId: Guid, req: AttachGuidanceRequest): Task&lt;IActionResult&gt;"
    ]
    add_class("ctrl_disease_zone", "DiseaseZoneController", "controller", ["- _sender: ISender"], c2_methods, 600, 90, 490, 175)

    c3_methods = [
        "+ ApproveSurveyResult(farmId: Guid, surveyResultId: Guid, req: ApproveRequest): Task&lt;IActionResult&gt;",
        "+ PublishSurveyResult(farmId: Guid, surveyResultId: Guid, req: PublishRequest): Task&lt;IActionResult&gt;",
        "+ GetPublishedSurveyResult(farmId: Guid, surveyResultId: Guid): Task&lt;IActionResult&gt;"
    ]
    add_class("ctrl_publication", "SurveyResultPublicationController", "controller", ["- _sender: ISender"], c3_methods, 1150, 90, 480, 140)

    # 2. INTERFACES
    i1_methods = [
        "+ TriggerDetectionAsync(farmId: Guid, missionId: Guid, ct: CancellationToken): Task&lt;DetectDiseasesResultDto&gt;",
        "+ GetFarmPlantHealthMapAsync(farmId: Guid, tenantId: Guid?, ct: CancellationToken): Task&lt;FarmPlantHealthMapResultDto&gt;",
        "+ ReviewSuspectedDetectionAsync(farmId: Guid, detectionId: Guid, decision: ReviewDecision, note: string): Task&lt;ReviewResultDto&gt;",
        "+ ResolveFlaggedPoleAsync(farmId: Guid, detectionId: Guid, action: FlagAction, targetPlantId: Guid?): Task&lt;FlagResultDto&gt;"
    ]
    add_class("intf_health", "IPlantHealthAnalysisService", "interface", [], i1_methods, 60, 310, 480, 135, fill="#F8FAFC")

    i2_methods = [
        "+ GetDiseaseZonesBySurveyAsync(farmId: Guid, surveyResultId: Guid, ct: CancellationToken): Task&lt;List&lt;DiseaseZoneDto&gt;&gt;",
        "+ MergeZonesAsync(farmId: Guid, zoneIds: List&lt;Guid&gt;, newZoneName: string, ct: CancellationToken): Task&lt;Guid&gt;",
        "+ UpdateZoneBoundaryAsync(farmId: Guid, zoneId: Guid, newPolygon: Polygon, ct: CancellationToken): Task&lt;DiseaseZoneDto&gt;",
        "+ UpdateMembershipsAsync(farmId: Guid, zoneId: Guid, assignedPlantIds: List&lt;Guid&gt;): Task&lt;int&gt;",
        "+ AttachGuidanceAsync(farmId: Guid, zoneId: Guid, conditionId: Guid, customNote: string): Task&lt;TreatmentGuidanceDto&gt;"
    ]
    add_class("intf_zone", "IDiseaseZoneService", "interface", [], i2_methods, 600, 310, 490, 150, fill="#F8FAFC")

    i3_methods = [
        "+ ApproveResultAsync(farmId: Guid, surveyResultId: Guid, managerId: Guid, notes: string): Task&lt;SurveyResultDto&gt;",
        "+ PublishResultAsync(farmId: Guid, surveyResultId: Guid, managerId: Guid): Task&lt;SurveyResultDto&gt;",
        "+ GetPublishedReportAsync(farmId: Guid, surveyResultId: Guid, tenantId: Guid): Task&lt;PublishedSurveyReportDto&gt;"
    ]
    add_class("intf_pub", "ISurveyResultPublicationService", "interface", [], i3_methods, 1150, 310, 480, 120, fill="#F8FAFC")

    # 3. IMPLEMENTATION SERVICES
    s1_fields = [
        "- _dbContext: IAgriDroneDbContext",
        "- _storageService: IObjectStorageService",
        "- _logger: ILogger&lt;PlantHealthAnalysisService&gt;"
    ]
    s1_methods = [
        "+ TriggerDetectionAsync(farmId: Guid, missionId: Guid, ct: CancellationToken): Task&lt;DetectDiseasesResultDto&gt;",
        "+ GetFarmPlantHealthMapAsync(farmId: Guid, tenantId: Guid?, ct: CancellationToken): Task&lt;FarmPlantHealthMapResultDto&gt;",
        "+ ReviewSuspectedDetectionAsync(farmId: Guid, detectionId: Guid, decision: ReviewDecision, note: string): Task&lt;ReviewResultDto&gt;",
        "+ ResolveFlaggedPoleAsync(farmId: Guid, detectionId: Guid, action: FlagAction, targetPlantId: Guid?): Task&lt;FlagResultDto&gt;",
        "- CalculateHaversineDistance(lat1: double, lon1: double, lat2: double, lon2: double): double",
        "- MatchDetectionsToPlants(detections: List&lt;RawDetectionDto&gt;, activePlants: List&lt;Plant&gt;): ValueTuple"
    ]
    add_class("svc_health", "PlantHealthAnalysisService", "service", s1_fields, s1_methods, 60, 500, 480, 185)

    s2_fields = [
        "- _diseaseZoneRepo: IDiseaseZoneRepository",
        "- _plantRepo: IPlantRepository",
        "- _uow: IUnitOfWork"
    ]
    s2_methods = [
        "+ GetDiseaseZonesBySurveyAsync(farmId: Guid, surveyResultId: Guid, ct: CancellationToken): Task&lt;List&lt;DiseaseZoneDto&gt;&gt;",
        "+ MergeZonesAsync(farmId: Guid, zoneIds: List&lt;Guid&gt;, newZoneName: string, ct: CancellationToken): Task&lt;Guid&gt;",
        "+ UpdateZoneBoundaryAsync(farmId: Guid, zoneId: Guid, newPolygon: Polygon, ct: CancellationToken): Task&lt;DiseaseZoneDto&gt;",
        "+ UpdateMembershipsAsync(farmId: Guid, zoneId: Guid, assignedPlantIds: List&lt;Guid&gt;): Task&lt;int&gt;",
        "+ AttachGuidanceAsync(farmId: Guid, zoneId: Guid, conditionId: Guid, customNote: string): Task&lt;TreatmentGuidanceDto&gt;",
        "- RecalculateEnclosedPlants(polygon: Polygon, farmPlants: List&lt;Plant&gt;): List&lt;Guid&gt;"
    ]
    add_class("svc_zone", "DiseaseZoneService", "service", s2_fields, s2_methods, 600, 500, 490, 185)

    s3_fields = [
        "- _surveyResultRepo: ISurveyResultRepository",
        "- _eventPublisher: IPlantHealthEventPublisher",
        "- _uow: IUnitOfWork"
    ]
    s3_methods = [
        "+ ApproveResultAsync(farmId: Guid, surveyResultId: Guid, managerId: Guid, notes: string): Task&lt;SurveyResultDto&gt;",
        "+ PublishResultAsync(farmId: Guid, surveyResultId: Guid, managerId: Guid): Task&lt;SurveyResultDto&gt;",
        "+ GetPublishedReportAsync(farmId: Guid, surveyResultId: Guid, tenantId: Guid): Task&lt;PublishedSurveyReportDto&gt;",
        "- ValidateAllPendingFlagsResolved(surveyResultId: Guid): Task&lt;bool&gt;",
        "- FreezeSurveyResultSnapshot(result: SurveyResult): void"
    ]
    add_class("svc_pub", "SurveyResultPublicationService", "service", s3_fields, s3_methods, 1150, 500, 480, 175)

    # 4. INFRASTRUCTURE SERVICES
    add_class("intf_storage", "IObjectStorageService", "interface", [], [
        "+ GetPresignedDownloadUrlAsync(bucket: string, key: string, expiry: TimeSpan): Task&lt;string&gt;",
        "+ UploadCropImageAsync(bucket: string, key: string, data: byte[], mime: string): Task&lt;string&gt;"
    ], 60, 740, 480, 105, fill="#F8FAFC")

    add_class("intf_event_pub", "IPlantHealthEventPublisher", "interface", [], [
        "+ PublishSurveyResultPublishedEventAsync(evt: SurveyResultPublishedIntegrationEvent): Task",
        "+ PublishDiseaseAlertNotificationAsync(evt: DiseaseAlertNotificationEvent): Task"
    ], 1150, 740, 480, 105, fill="#F8FAFC")

    # 5. DOMAIN ENTITIES & ENUMS
    e1_fields = [
        "+ Id: Guid",
        "+ MissionId: Guid",
        "+ PlantId: Guid?",
        "+ DiseaseName: string",
        "+ Confidence: double",
        "+ CropImageUrl: string",
        "+ ReviewStatus: ObservationReviewStatus",
        "+ ReviewDecision: ConditionReviewDecision?",
        "+ ReviewNotes: string"
    ]
    e1_methods = [
        "+ ConfirmReview(reviewerId: Guid): void",
        "+ RejectReview(reviewerId: Guid, reason: string): void",
        "+ SnapToPlant(plantId: Guid): void"
    ]
    add_class("ent_detection", "ConditionDetection", "entity", e1_fields, e1_methods, 60, 890, 360, 260)

    e2_fields = [
        "+ Id: Guid",
        "+ SurveyResultId: Guid",
        "+ Name: string",
        "+ Boundary: Polygon",
        "+ Status: DiseaseZoneStatus",
        "+ SeverityLevel: DiseaseSeverity",
        "+ TotalEnclosedTrees: int"
    ]
    e2_methods = [
        "+ UpdateBoundary(newBoundary: Polygon): void",
        "+ MergeWith(otherZone: DiseaseZone): void",
        "+ AssignPlant(plantId: Guid, kind: DiseaseZoneMembershipKind): void"
    ]
    add_class("ent_zone", "DiseaseZone", "entity", e2_fields, e2_methods, 460, 890, 350, 240)

    e2_member_fields = [
        "+ DiseaseZoneId: Guid",
        "+ PlantId: Guid",
        "+ MembershipKind: DiseaseZoneMembershipKind"
    ]
    add_class("ent_zone_member", "DiseaseZonePlantMembership", "entity", e2_member_fields, [], 460, 1170, 350, 95)

    e3_fields = [
        "+ Id: Guid",
        "+ PlantConditionId: Guid",
        "+ SeverityLevel: DiseaseSeverity",
        "+ ActiveIngredients: string",
        "+ DosageInstructions: string",
        "+ MitigationSteps: string",
        "+ SafetyIntervalDays: int"
    ]
    add_class("ent_treatment", "TreatmentRecommendation", "entity", e3_fields, [], 850, 890, 260, 190)

    e4_fields = [
        "+ Id: Guid",
        "+ SurveyOrderId: Guid",
        "+ Status: SurveyResultStatus",
        "+ ApprovedAt: DateTimeOffset?",
        "+ ApprovedBy: Guid?",
        "+ PublishedAt: DateTimeOffset?",
        "+ PublishedBy: Guid?",
        "+ TotalPlantsAnalyzed: int",
        "+ InfectedPlantsCount: int",
        "+ ApprovalNotes: string"
    ]
    e4_methods = [
        "+ Approve(managerId: Guid, notes: string): void",
        "+ Publish(managerId: Guid): void",
        "+ MarkCompleted(): void"
    ]
    add_class("ent_survey_result", "SurveyResult", "entity", e4_fields, e4_methods, 1150, 890, 480, 270)

    # ENUMS
    add_class("enum_review_status", "ObservationReviewStatus", "enumeration", [], [
        "Pending", "Matched", "Confirmed", "Rejected"
    ], 60, 1190, 200, 110, fill="#F1F5F9")

    add_class("enum_zone_status", "DiseaseZoneStatus", "enumeration", [], [
        "Proposed", "Reviewed", "Published"
    ], 280, 1190, 150, 95, fill="#F1F5F9")

    add_class("enum_survey_status", "SurveyResultStatus", "enumeration", [], [
        "ReadyForProcessing", "AnalyzingPlantHealth", "PendingReview", "Approved", "Published", "Completed"
    ], 1150, 1200, 230, 140, fill="#F1F5F9")

    add_class("enum_member_kind", "DiseaseZoneMembershipKind", "enumeration", [], [
        "CoreOutbreak", "BufferZone"
    ], 850, 1120, 240, 80, fill="#F1F5F9")

    # EDGES
    def add_edge(src, tgt, style, label="", edge_id=None):
        if not edge_id:
            edge_id = f"e_{src}_{tgt}"
        xml.append(f'        <mxCell id="{edge_id}" parent="1" value="{label}" style="{style}" edge="1">')
        xml.append(f'          <mxGeometry relative="1" as="geometry">')
        xml.append(f'            <mxPoint as="sourcePoint" />')
        xml.append(f'            <mxPoint as="targetPoint" />')
        xml.append(f'          </mxGeometry>')
        xml.append(f'        </mxCell>')

    # 1. Controller -> Interface (Dependency: dashed open arrow)
    dep_style = "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;dashed=1;endArrow=open;endFill=0;strokeColor=#000000;fontSize=10;"
    xml.append(f'        <mxCell id="e_c1_i1" parent="1" value="uses" style="{dep_style}" edge="1" source="ctrl_plant_health" target="intf_health"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_c2_i2" parent="1" value="uses" style="{dep_style}" edge="1" source="ctrl_disease_zone" target="intf_zone"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_c3_i3" parent="1" value="uses" style="{dep_style}" edge="1" source="ctrl_publication" target="intf_pub"><mxGeometry relative="1" as="geometry"/></mxCell>')

    # 2. Service -> Interface (Realization: dashed closed triangle)
    impl_style = "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;dashed=1;endArrow=block;endFill=0;strokeColor=#000000;strokeWidth=1.2;"
    xml.append(f'        <mxCell id="e_s1_i1" parent="1" value="" style="{impl_style}" edge="1" source="svc_health" target="intf_health"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_s2_i2" parent="1" value="" style="{impl_style}" edge="1" source="svc_zone" target="intf_zone"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_s3_i3" parent="1" value="" style="{impl_style}" edge="1" source="svc_pub" target="intf_pub"><mxGeometry relative="1" as="geometry"/></mxCell>')

    # 3. Services -> Infrastructure
    xml.append(f'        <mxCell id="e_s1_st" parent="1" value="uses" style="{dep_style}" edge="1" source="svc_health" target="intf_storage"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_s3_mq" parent="1" value="publishes" style="{dep_style}" edge="1" source="svc_pub" target="intf_event_pub"><mxGeometry relative="1" as="geometry"/></mxCell>')

    # 4. Services -> Entities
    xml.append(f'        <mxCell id="e_s1_e1" parent="1" value="manages" style="{dep_style}" edge="1" source="svc_health" target="ent_detection"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_s2_e2" parent="1" value="manages" style="{dep_style}" edge="1" source="svc_zone" target="ent_zone"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_s3_e4" parent="1" value="manages" style="{dep_style}" edge="1" source="svc_pub" target="ent_survey_result"><mxGeometry relative="1" as="geometry"/></mxCell>')

    # 5. Entity Relationships
    # SurveyResult *-- DiseaseZone (Composition)
    comp_style = "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;startArrow=diamond;startFill=1;endArrow=none;strokeColor=#000000;fontSize=10;"
    xml.append(f'        <mxCell id="e_sr_dz" parent="1" value="1          0..*" style="{comp_style}" edge="1" source="ent_survey_result" target="ent_zone"><mxGeometry relative="1" as="geometry"/></mxCell>')

    # DiseaseZone *-- DiseaseZonePlantMembership
    xml.append(f'        <mxCell id="e_dz_dzpm" parent="1" value="1          0..*" style="{comp_style}" edge="1" source="ent_zone" target="ent_zone_member"><mxGeometry relative="1" as="geometry"/></mxCell>')

    # DiseaseZone ..> TreatmentRecommendation (References)
    xml.append(f'        <mxCell id="e_dz_tr" parent="1" value="references" style="{dep_style}" edge="1" source="ent_zone" target="ent_treatment"><mxGeometry relative="1" as="geometry"/></mxCell>')

    # Entity -> Enum
    xml.append(f'        <mxCell id="e_cd_obs" parent="1" value="has" style="{dep_style}" edge="1" source="ent_detection" target="enum_review_status"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_dz_st" parent="1" value="has" style="{dep_style}" edge="1" source="ent_zone" target="enum_zone_status"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_sr_st" parent="1" value="has" style="{dep_style}" edge="1" source="ent_survey_result" target="enum_survey_status"><mxGeometry relative="1" as="geometry"/></mxCell>')
    xml.append(f'        <mxCell id="e_dzm_kind" parent="1" value="has" style="{dep_style}" edge="1" source="ent_zone_member" target="enum_member_kind"><mxGeometry relative="1" as="geometry"/></mxCell>')

    xml.append("      </root>")
    xml.append("    </mxGraphModel>")
    xml.append("  </diagram>")
    xml.append("</mxfile>")

    return "\n".join(xml)

content = generate_class_diagram_drawio()
with open(r"d:\capstone\AgriDrone-BE\docs\diagrams\Chinh_Plant_Health_Class_Diagram.drawio", "w", encoding="utf-8") as f:
    f.write(content)
print("Saved Chinh_Plant_Health_Class_Diagram.drawio successfully!")


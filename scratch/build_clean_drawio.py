import xml.etree.ElementTree as ET
import html

def build_real_drawio():
    root = ET.Element("mxfile", host="app.diagrams.net", version="24.0.0")
    diagram = ET.SubElement(root, "diagram", id="chinh_class_dia", name="Figure 3.4.1 Class Diagram - Plant Health Analysis & Publication")
    graph_model = ET.SubElement(diagram, "mxGraphModel", dx="1600", dy="1200", grid="1", gridSize="10", guides="1", tooltips="1", connect="1", arrows="1", fold="1", page="1", pageScale="1", pageWidth="1800", pageHeight="1450", math="0", shadow="0")
    root_cell = ET.SubElement(graph_model, "root")
    
    ET.SubElement(root_cell, "mxCell", id="0")
    ET.SubElement(root_cell, "mxCell", id="1", parent="0")

    def add_node(node_id, value, style, x, y, w, h):
        cell = ET.SubElement(root_cell, "mxCell", id=node_id, parent="1", value=value, style=style, vertex="1")
        ET.SubElement(cell, "mxGeometry", x=str(x), y=str(y), width=str(w), height=str(h), **{"as": "geometry"})
        return cell

    def add_edge(edge_id, source_id, target_id, label, style):
        cell = ET.SubElement(root_cell, "mxCell", id=edge_id, parent="1", source=source_id, target=target_id, value=label, style=style, edge="1")
        ET.SubElement(cell, "mxGeometry", relative="1", **{"as": "geometry"})
        return cell

    # Title Banner
    title_html = (
        "<b>Figure 3.4.1: Class Diagram — Plant Health Analysis &amp; Publication Package</b><br/>"
        "<font style='font-size:11px; font-weight:normal; color:#222222;'>"
        "AgriDrone Backend Service · Exact Architecture Model (ASP.NET Core 8 / MediatR CQRS / Clean Architecture)"
        "</font>"
    )
    add_node("title", title_html, "text;html=1;align=center;verticalAlign=middle;whiteSpace=wrap;rounded=1;fillColor=#FFFFFF;strokeColor=#000000;fontSize=13;fontColor=#000000;", 60, 20, 1620, 45)

    def format_class_html(stereotype, name, fields, methods):
        lines = []
        if stereotype:
            lines.append(f"<div style='text-align:center;'><font color='#000000' style='font-size:10px;'>&lt;&lt;{html.escape(stereotype)}&gt;&gt;</font></div>")
        lines.append(f"<div style='text-align:center; color:#000000;'><b>{html.escape(name)}</b></div>")
        lines.append("<hr size='1' style='border-top: 1px solid #000000; margin: 4px 0;'/>")
        
        if fields:
            f_lines = "<br/>".join([
                "+ " + html.escape(f) if not f.startswith(('-', '+', '#')) else f[0] + " " + html.escape(f[1:].strip())
                for f in fields
            ])
            lines.append(f"<div style='text-align:left; font-size:10px; margin-left:4px; color:#000000;'>{f_lines}</div>")
            lines.append("<hr size='1' style='border-top: 1px solid #000000; margin: 4px 0;'/>")
        else:
            lines.append("<hr size='1' style='border-top: 1px solid #000000; margin: 4px 0;'/>")
            
        if methods:
            m_lines = "<br/>".join([
                "+ " + html.escape(m) if not m.startswith(('-', '+', '#')) else m[0] + " " + html.escape(m[1:].strip())
                for m in methods
            ])
            lines.append(f"<div style='text-align:left; font-size:10px; margin-left:4px; color:#000000;'>{m_lines}</div>")
            
        return "".join(lines)

    mono_style = "rounded=0;whiteSpace=wrap;html=1;align=left;verticalAlign=top;spacingLeft=6;spacingRight=6;spacingTop=6;fillColor=#FFFFFF;strokeColor=#000000;strokeWidth=1;fontSize=11;fontColor=#000000;"
    enum_style = "rounded=0;whiteSpace=wrap;html=1;align=left;verticalAlign=top;spacingLeft=6;spacingRight=6;spacingTop=6;fillColor=#FFFFFF;strokeColor=#000000;strokeWidth=1;fontSize=10;fontColor=#000000;"

    # ==========================================
    # 1. CONTROLLERS (Row 1: y = 85)
    # ==========================================
    c1_html = format_class_html(
        "controller", "MissionOrthophotoController",
        ["- sender: ISender", "- managerAccessService: ISystemManagerAccessService"],
        [
            "+ DetectDiseases(farmId: Guid, missionId: Guid, req: DetectMissionDiseasesRequest?, ct: CancellationToken): Task<IResult>"
        ]
    )
    add_node("ctrl_mission", c1_html, mono_style, 60, 85, 480, 130)

    c2_html = format_class_html(
        "controller", "FarmPlantHealthController",
        ["- sender: ISender"],
        [
            "+ GetPlantHealthMap(farmId: Guid, tenantId: Guid?, ct: CancellationToken): Task<IResult>",
            "+ ReviewSuspectedDisease(farmId: Guid, detectionId: Guid, req: ReviewRequest, ct: CancellationToken): Task<IResult>",
            "+ ResolveFlaggedLocation(farmId: Guid, detectionId: Guid, req: ResolveFlagRequest, ct: CancellationToken): Task<IResult>",
            "+ GetPublishedSurveyResult(farmId: Guid, surveyResultId: Guid, ct: CancellationToken): Task<IResult>"
        ]
    )
    add_node("ctrl_plant_health", c2_html, mono_style, 590, 85, 530, 180)

    c3_html = format_class_html(
        "controller", "SystemPlantHealthGuidanceController",
        ["- sender: ISender"],
        [
            "+ GetHistory(ct: CancellationToken): Task<IResult>",
            "+ Create(req: CreateSourcedGuidanceRequest, ct: CancellationToken): Task<IResult>",
            "+ Activate(guidanceId: Guid, req: ChangeSourcedGuidanceStatusRequest, ct: CancellationToken): Task<IResult>"
        ]
    )
    add_node("ctrl_guidance", c3_html, mono_style, 1170, 85, 510, 145)

    # ==========================================
    # 2. CQRS HANDLERS & DOMAIN SERVICES (Row 2: y = 300)
    # ==========================================
    h1_html = format_class_html(
        "handler", "DetectMissionDiseasesCommandHandler",
        [
            "- missionRepository: IDroneMissionRepository",
            "- dbContext: MissionsDbContext",
            "- metashapeService: IMetashapeService",
            "- managerAccessService: ISystemManagerAccessService",
            "- executionContext: IExecutionContext"
        ],
        [
            "+ Handle(request: DetectMissionDiseasesCommand, ct: CancellationToken): Task<Result<DetectMissionDiseasesResult>>",
            "- CalculateHaversineDistance(lat1: double, lon1: double, lat2: double, lon2: double): double",
            "- FindConnectedComponents(trees: List<DiseasedTree>, radiusMeters: double, minTrees: int): List<OutbreakCluster>"
        ]
    )
    add_node("handler_detect", h1_html, mono_style, 60, 300, 490, 205)

    h2_html = format_class_html(
        "handler", "GetFarmPlantHealthMapQueryHandler",
        [
            "- missionsDbContext: MissionsDbContext",
            "- objectStorage: IObjectStorage"
        ],
        [
            "+ Handle(query: GetFarmPlantHealthMapQuery, ct: CancellationToken): Task<Result<FarmPlantHealthMapResult>>"
        ]
    )
    add_node("handler_map", h2_html, mono_style, 590, 300, 530, 125)

    ds1_html = format_class_html(
        "domain service", "DiseaseZonePublicationService",
        [],
        [
            "+ Publish(reviewedZone: DiseaseZone, currentPublished: DiseaseZone?, publishedBy: Guid, publishedAt: DateTimeOffset): void"
        ]
    )
    add_node("ds_disease_zone", ds1_html, mono_style, 1170, 300, 510, 110)

    # ==========================================
    # 3. INTERFACES, REPOSITORIES & GUARDS (Row 3: y = 535)
    # ==========================================
    guard_html = format_class_html(
        "domain guard", "DiseaseZoneGeometryGuard",
        [],
        [
            "+ EnsureValidPolygon(geometry: Geometry): void",
            "+ EnsureWithinBoundary(zoneGeometry: Geometry, farmBoundary: Geometry): void"
        ]
    )
    add_node("guard_zone", guard_html, mono_style, 60, 545, 490, 110)

    repo_intf_html = format_class_html(
        "interface", "IDiseaseZoneRepository",
        [],
        [
            "+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<DiseaseZone?>",
            "+ AddAsync(zone: DiseaseZone, ct: CancellationToken): Task",
            "+ UpdateAsync(zone: DiseaseZone, ct: CancellationToken): Task"
        ]
    )
    add_node("intf_dz_repo", repo_intf_html, mono_style, 590, 465, 530, 115)

    repo_impl_html = format_class_html(
        "repository", "DiseaseZoneRepository",
        ["- _dbContext: PlantsDbContext"],
        [
            "+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<DiseaseZone?>",
            "+ AddAsync(zone: DiseaseZone, ct: CancellationToken): Task",
            "+ UpdateAsync(zone: DiseaseZone, ct: CancellationToken): Task"
        ]
    )
    add_node("impl_dz_repo", repo_impl_html, mono_style, 590, 620, 530, 120)

    mq_intf_html = format_class_html(
        "interface", "IRabbitMqPublisher",
        [],
        [
            "+ PublishAsync<T>(exchange: string, routingKey: string, message: T, ct: CancellationToken): Task"
        ]
    )
    add_node("intf_mq", mq_intf_html, mono_style, 1170, 445, 510, 95)

    storage_intf_html = format_class_html(
        "interface", "IObjectStorage",
        [],
        [
            "+ GetPresignedUrlAsync(bucket: string, objectKey: string, expiry: TimeSpan): Task<string>",
            "+ PutObjectAsync(bucket: string, objectKey: string, data: Stream, contentType: string): Task"
        ]
    )
    add_node("intf_storage", storage_intf_html, mono_style, 1170, 580, 510, 110)

    # ==========================================
    # 4. DOMAIN ENTITIES & AGGREGATES (Row 4: y = 780)
    # ==========================================
    e1_html = format_class_html(
        "aggregate root", "DiseaseZone",
        [
            "+ Id: Guid",
            "+ SurveyOrderId: Guid",
            "+ SurveyResultId: Guid",
            "+ FarmId: Guid",
            "+ Boundary: Polygon",
            "+ Status: DiseaseZoneStatus",
            "+ SeverityLevel: string",
            "+ TotalEnclosedTrees: int"
        ],
        [
            "+ CreateProposal(proposal: DiseaseZoneProposal): DiseaseZone",
            "+ Review(reviewedGeometry: Polygon, ...): void",
            "+ Reject(reviewedBy: Guid, reason: string): void",
            "+ Publish(publishedBy: Guid, publishedAt: DateTimeOffset): void",
            "+ Supersede(supersededByZoneId: Guid, at: DateTimeOffset): void"
        ]
    )
    add_node("ent_zone", e1_html, mono_style, 60, 780, 430, 275)

    e2_html = format_class_html(
        "entity", "DiseaseZonePlantMembership",
        [
            "+ DiseaseZoneId: Guid",
            "+ PlantId: Guid",
            "+ MembershipKind: DiseaseZoneMembershipKind"
        ],
        []
    )
    add_node("ent_member", e2_html, mono_style, 540, 780, 310, 115)

    e3_html = format_class_html(
        "entity", "SurveyResult",
        [
            "+ Id: Guid",
            "+ SurveyOrderId: Guid",
            "+ Status: SurveyResultStatus",
            "+ ApprovedAt: DateTimeOffset?",
            "+ ApprovedBy: Guid?",
            "+ PublishedAt: DateTimeOffset?",
            "+ PublishedBy: Guid?",
            "+ TotalPlantsAnalyzed: int",
            "+ InfectedPlantsCount: int"
        ],
        [
            "+ Approve(managerId: Guid): void",
            "+ Publish(managerId: Guid): void"
        ]
    )
    add_node("ent_survey", e3_html, mono_style, 900, 780, 410, 255)

    # ==========================================
    # 5. ENUMS (Row 5: y = 1100)
    # ==========================================
    add_node("enum_dz_status", format_class_html("enum", "DiseaseZoneStatus", [], ["Proposed", "Reviewed", "Published", "Rejected", "Superseded"]), enum_style, 60, 1100, 190, 120)
    add_node("enum_member_kind", format_class_html("enum", "DiseaseZoneMembershipKind", [], ["CoreOutbreak", "BufferZone"]), enum_style, 280, 1100, 210, 85)
    add_node("enum_survey_status", format_class_html("enum", "SurveyResultStatus", [], ["ReadyForProcessing", "AnalyzingPlantHealth", "PendingReview", "Approved", "Published", "Completed"]), enum_style, 900, 1100, 260, 140)

    # ==========================================
    # EDGES & RELATIONSHIPS
    # ==========================================
    dep_style = "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;dashed=1;endArrow=open;endFill=0;strokeColor=#000000;fontSize=10;fontColor=#000000;"
    impl_style = "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;dashed=1;endArrow=block;endFill=0;strokeColor=#000000;strokeWidth=1.2;"
    comp_style = "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;startArrow=diamond;startFill=1;endArrow=none;strokeColor=#000000;fontSize=10;fontColor=#000000;"

    # Controllers -> Handlers
    add_edge("e_c1_h1", "ctrl_mission", "handler_detect", "sends command", dep_style)
    add_edge("e_c2_h2", "ctrl_plant_health", "handler_map", "sends query", dep_style)
    add_edge("e_h2_st", "handler_map", "intf_storage", "uses", dep_style)

    # Repository Realization
    add_edge("e_repo_impl", "impl_dz_repo", "intf_dz_repo", "", impl_style)
    add_edge("e_ds_repo", "ds_disease_zone", "ent_zone", "publishes", dep_style)

    # Entities
    add_edge("e_zone_mem", "ent_zone", "ent_member", "1       0..*", comp_style)
    add_edge("e_survey_zone", "ent_survey", "ent_zone", "1       0..*", comp_style)
    add_edge("e_zone_st", "ent_zone", "enum_dz_status", "has", dep_style)
    add_edge("e_mem_kind", "ent_member", "enum_member_kind", "has", dep_style)
    add_edge("e_surv_st", "ent_survey", "enum_survey_status", "has", dep_style)

    target_path = r"d:\capstone\AgriDrone-BE\docs\diagrams\Chinh_Plant_Health_Class_Diagram.drawio"
    tree = ET.ElementTree(root)
    ET.indent(tree, space="  ", level=0)
    tree.write(target_path, encoding="utf-8", xml_declaration=True)
    print("Clean drawio built successfully at:", target_path)

if __name__ == "__main__":
    build_real_drawio()

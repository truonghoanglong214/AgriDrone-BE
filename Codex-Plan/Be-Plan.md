3. Register content of Capstone Project

(*) 3.1. Capstone Project name:

3.1.1. English: AgriDrone: Smart Dragon Fruit Farm Management System Using Drone and AI

3.1.2. Vietnamese: AgriDrone: Hệ thống quản lý vườn thanh long thông minh sử dụng Drone và AI

Abbreviation: AgriDrone

(*) 3.2. Main proposal content (including result and product)

a) Context:

Dragon fruit farms may contain thousands of concrete poles arranged in regular rows. Across several hectares, manual counting and repeated visual inspection are time-consuming, labour-intensive, and difficult to perform consistently. Observations are also rarely tied to a persistent identity for each physical plant, so owners cannot reliably determine whether a specific plant is improving, deteriorating, repeatedly showing the same problem, or has been removed and replaced across survey dates.

Drone imagery and computer vision can make large-area, plant-level surveying practical. However, flight planning, drone operation, geospatial processing, AI interpretation, disease-area summarization, and maintenance of plant-level history require technical capabilities that many growers do not have. AgriDrone therefore provides a managed drone-and-AI survey service: farm owners request a survey, AgriDrone personnel organize and perform the required flight(s), AI processes the collected data, a SystemManager verifies the outputs, and verified results are published to the owner through a digital portal.

• Plant inventory is difficult to maintain accurately when counting is performed manually and plants may be added, removed, or replanted; inventory changes also need an auditable owner-report and verification process.

• Manual health inspection does not scale well across large orchards and cannot be repeated frequently at plant level.

• Individual disease detections are difficult for owners to act on when many affected plants occur close together; the system should summarize nearby diseased plants into reviewable Disease Zones.

• Disease findings are more useful when accompanied by consistent expert-validated treatment guidance rather than unverified free-form AI advice.

• Plant-level longitudinal records are limited when observations from different dates cannot be associated with the same physical plant.

• Repeated surveys require reliable plant re-identification because consumer-grade GPS error may be comparable to the spacing between neighbouring poles and may also place a detection outside the correct Farm boundary or toward a neighbouring Farm.

• Farm owners need a simple service workflow without having to operate drones, manage AI models, or verify raw AI detections themselves.

• The platform must connect survey requests, customer onboarding, Farm-boundary verification, baseline pole counting, per-pole pricing, appointments, payments, drone missions, AI processing, disease-zone review, treatment recommendations, plant-change verification, and result publication.

b) Proposed Solutions:

AgriDrone is a service-oriented dragon fruit farm survey and monitoring platform combining drone-based data collection, computer vision, geospatial processing, expert-validated guidance, and human verification. A public applicant can view available Survey Services and submit a Survey Request containing contact information, farm information, approximate area, map location, estimated pole count if known, preferred time, and notes. SystemAdmin reviews the request. For a new customer, approval creates the Tenant, Farm and SurveyOrder, assigns one available and flight-qualified SystemManager to the Farm, and sends an invitation for the farm owner to activate a TenantOwner account. For an existing TenantOwner who wants to register another Farm, the owner submits a New Farm Survey Request from the portal; after SystemAdmin approval, the system creates only the new Farm and SurveyOrder under the existing Tenant, assigns an available and flight-qualified SystemManager to that Farm, and keeps the existing Tenant/account unchanged.

The assigned SystemManager verifies the Farm, the approved FarmBoundary polygon, and the survey scope, then coordinates the required appointment(s). Each Survey Service has a configurable PricePerPole. For a Farm without a confirmed baseline pole inventory, AgriDrone first performs the mandatory Baseline Mapping Mission without charging it as a separate Survey Service. AI detects and counts poles, and the SystemManager reviews the mapping output and confirms the active pole count within the approved scope. The SurveyOrder final price is then calculated as FinalPrice = ConfirmedSurveyPoleCount × PricePerPole. The applicable PricePerPole and ConfirmedSurveyPoleCount are stored as pricing snapshots so later price or inventory changes do not silently alter an already confirmed order. Payment is required before the paid Plant Health or Harvest Readiness mission is executed, but the prerequisite Baseline Mapping Mission may be performed before payment because it is required to establish the confirmed pole count used for pricing.

For a Farm that does not yet have an approved base map and confirmed pole inventory, the first SurveyOrder begins with a mandatory Baseline Mapping Mission before the selected paid Survey Service mission. The Baseline Mapping Mission detects, de-duplicates, counts, geolocates, and organizes poles; supports verification of the FarmBoundary; and establishes the initial persistent Digital Plant Profiles for the Farm. Once reviewed and published, this base map and confirmed active-pole inventory are reused across later SurveyOrders and Survey Services. Subsequent surveys re-identify detected plants against existing Digital Plant Profiles rather than generating a new Farm map. For service-specific longitudinal analysis, verified results are compared with the most recent compatible completed survey of the same Survey Service.

• Survey Request & Customer Onboarding: public service catalogue/request, approval/rejection, new-customer Tenant/Farm provisioning, existing-Tenant New Farm registration, SystemManager assignment, and TenantOwner invitation when onboarding a new customer.

• Survey Services: Plant Health Survey and Harvest Readiness Survey. Follow-up is not a separate service; it is a repeated survey of the same service after an initial completed survey exists. A Baseline Mapping Mission is not sold or charged as a separate Survey Service in the MVP; it is an operational prerequisite automatically performed when a Farm does not yet have an approved base map and confirmed pole inventory.

• Per-Pole Pricing: each service has a configurable PricePerPole; the confirmed number of active poles in the approved survey scope is used to calculate the final price, and the SurveyOrder stores PricePerPole and pole-count pricing snapshots.

• Survey Order, Appointment & Payment Management: tracks service, scope, confirmed pole count, price, appointments, payment, and operational status. For a new/unmapped Farm, Baseline Mapping is completed first to establish the confirmed pole count; payment is then a hard gate before the selected paid Survey Service mission.

• System Drone & Mission Management: AgriDrone centrally manages drones; SystemManagers prepare missions, select an available drone, perform pre-flight checks and flights, and upload imagery/GPS/telemetry.

• Plant Mapping & Counting: detects poles, de-duplicates detections across overlapping frames, geolocates plants, validates detections against the approved FarmBoundary, and creates the Farm base map and initial active plant inventory during Baseline Mapping. Detections outside the boundary or suspiciously near it are flagged as OutOfBoundary/NeedsReview and are not automatically assigned to the Farm. Subsequent surveys reuse the existing base map and match observations to existing Digital Plant Profiles instead of creating a new map.

• Plant Re-identification: matches plants from repeated surveys to persistent Digital Plant Profiles using position plus orchard row/column structure, while using the approved FarmBoundary as a guardrail against cross-Farm assignment caused by GPS drift.

• Plant Health Analysis: detects selected visually distinguishable diseases and grades severity as Healthy, Mild, Moderate, or Severe, while addressing confusion with sunburn and mechanical scarring. For verified disease findings, the system selects an advisory treatment recommendation from an expert-validated catalogue mapped by disease and severity rather than generating unrestricted treatment advice.

• Disease Zone Generation: AI groups nearby diseased plants into proposed spatial clusters/polygons so owners can see concentrated affected areas. A proposed Disease Zone remains pending until a SystemManager reviews, corrects where necessary, and approves it for publication.

• Plant Inventory Change Workflow: TenantOwner may report that an existing plant/pole has been removed or replaced, or that a new plant has been added. The report remains pending until SystemManager verification. Old Digital Plant Profiles are retained for history and marked Removed/Inactive rather than physically deleted. A genuinely new plant receives a new Digital Plant Profile only after it is observed in a later survey and verified by the SystemManager.

• Harvest Readiness Survey: uses drone imagery and AI to assess defined visible maturity indicators and estimate whether the observed dragon fruit is ready for harvest according to the project's validated readiness criteria. The exact observable criteria, assessment granularity, ground-truth procedure, dataset requirements, and evaluation protocol must be established during domain study before the capability is claimed as validated.

• Human-in-the-Loop Verification: AI-generated mapping candidates, plant re-identification results, boundary exceptions, disease/severity findings, proposed Disease Zones, Harvest Readiness assessments, new-plant detections, and other service-specific outputs remain pending until SystemManager review, correction where necessary, and approval. Only approved results may be published to TenantOwner or used as official inventory changes.

• Digital Plant Profile & Monitoring Portal: stores persistent plant identity, location, lifecycle status, imagery, verified observations, disease-zone membership, advisory recommendations, and longitudinal survey history for TenantOwner viewing.

Project Scope Boundary / Out of Scope:

• Tenant self-operation of drones.

• Tenant-created Drone Missions.

• Tenant review or approval of raw AI results.

• Harvest recording, harvested quantity tracking, yield accounting, harvest-batch management, and post-harvest management. Harvest Readiness assessment based on visible imagery remains within the project scope.

• Worker management.

• Field verification task management.

• Automated spraying or treatment.

• Automatic execution of treatment recommendations; recommendations are advisory and must come from the expert-validated catalogue.

• Fully autonomous flight.

• Real-time AI inference while the drone is flying.

• Support for crop types other than dragon fruit.

• Coverage of all dragon-fruit diseases; the project is limited to a small selected disease set.

• RTK or centimetre-level positioning as a system requirement; the research explicitly targets low-cost consumer-grade positioning without requiring RTK.

c) Functional Requirements:

Public Applicant / Survey Requester:

• View available Survey Services and indicative price-per-pole information.

• Submit a Survey Request with contact information, Farm information, approximate area, map location/coordinates, estimated pole count if known, selected service, preferred time, and notes.

• Receive request acknowledgement and approval/rejection or onboarding communication.

SystemAdmin:

• Review Survey Requests and approve or reject them with a recorded reason. Approval must consider the validity of applicant/contact information, whether the Farm is a dragon fruit orchard located within the supported service area, preliminary legal and operational feasibility of drone flight at the requested location, and availability of an active and flight-qualified SystemManager.

• For a new customer approval, create Tenant, Farm and SurveyOrder, assign one active and flight-qualified SystemManager to the Farm, and initiate the TenantOwner invitation; for an approved New Farm request from an existing Tenant, create the new Farm and SurveyOrder under that Tenant without creating another Tenant or owner account, and assign one active and flight-qualified SystemManager to the new Farm.

• Manage SystemManager accounts, flight-qualification status, availability, and Farm assignment/reassignment.

• Manage the system-owned drone registry, including model/specifications, registration details, maintenance status, and operational status.

• Manage Survey Services and PricePerPole.

• Manage AI model versions, disease catalogue, severity definitions, confidence thresholds, and the expert-validated treatment recommendation catalogue mapped to disease and severity.

• Monitor requests, orders, payments, missions, processing jobs, storage, and system health.

SystemManager:

• View assigned Farms and verify Farm, approved FarmBoundary, Zone, and survey-scope information. A Farm shall have only one primary assigned SystemManager at a time; SystemAdmin may reassign the Farm when necessary, and the reassignment must be auditable.

• For a Farm without a confirmed baseline inventory, coordinate the Baseline Mapping appointment, review the detected poles, and confirm the active pole count in the approved survey scope. Confirm the applicable PricePerPole and final pricing inputs before requesting payment from TenantOwner.

• Monitor SurveyOrder, appointment, and payment status. The prerequisite Baseline Mapping Mission may be executed before payment to establish the confirmed pole count; the selected paid Survey Service mission must not be executed until its appointment is confirmed and the required payment is successfully confirmed.

• Prepare required mission(s), select an available system-owned drone, and complete pre-flight checks. If the Farm does not yet have an approved base map and confirmed pole inventory, prepare and perform the mandatory Baseline Mapping Mission first; after pole-count verification, pricing confirmation, and payment, prepare the selected Plant Health or Harvest Readiness mission.

• Perform the survey and upload imagery/video, GPS/telemetry, timestamps, and mission metadata.

• Monitor AI processing and review/correct plant matching, FarmBoundary exceptions, disease/severity findings, proposed Disease Zones, treatment-recommendation selection, and other service-specific results; verify alignment with the existing Farm base map rather than approving a newly generated map for every survey.

• Review TenantOwner-reported plant removals, replacements, and additions. Confirm valid removals/replacements as lifecycle changes without deleting historical profiles; on a later survey, verify detected new plants before a new Digital Plant Profile is created.

• For Follow-up Surveys, review re-identification and comparison with the most recent compatible completed survey of the same service.

• Approve and publish verified Survey Results.

TenantOwner:

• Accept the invitation and activate/link an account to the approved Tenant.

• View own Farms and current SurveyOrder status.

• Register an additional Farm under the existing Tenant by submitting a New Farm Survey Request with Farm information, approximate area, map location/coordinates, selected Survey Service, preferred time, and notes; after SystemAdmin approval, the new Farm is created under the same Tenant and assigned to a SystemManager.

• Confirm a proposed Baseline Mapping or paid Survey Service appointment, or request rescheduling.

• View confirmed survey scope, ConfirmedSurveyPoleCount, PricePerPole, final price and payment status, and complete payment after the confirmed pole count is available.

• View published FarmBoundary/Farm maps, approved Disease Zones, active plant count, Digital Plant Profiles, survey imagery, official service results, and expert-validated advisory treatment recommendations associated with verified disease findings.

• View survey history and longitudinal plant changes.

• Report plant inventory changes by marking an existing plant/pole as removed or replaced, or by reporting a newly added plant. The report is pending until SystemManager verification and does not directly delete or create an official Digital Plant Profile.

• Request another survey for an existing Farm; when the same service has a previous completed survey, the request is handled as a Follow-up Survey.

System / AI Processing Engine:

• Extract frames and associate them with GPS, timestamp, and mission metadata.

• Detect poles and track them across frames to reduce duplicate counts.

• Assign geographic positions and derive row/column indices; validate positions against the approved FarmBoundary and flag detections outside or suspiciously near the boundary as OutOfBoundary/NeedsReview instead of automatically assigning them to the Farm.

• For a Farm without an approved base map and confirmed pole inventory, process the Baseline Mapping Mission to detect, de-duplicate, count, geolocate, and organize detected poles into mapping candidates for SystemManager review before creating the initial Farm base map and Digital Plant Profiles. For subsequent surveys, reuse the existing Farm base map, match newly detected plants to existing Digital Plant Profiles, and identify unmatched/new plants as pending candidates without automatically creating a new profile or rebuilding the Farm map.

• Perform analysis required by the selected Survey Service. For Plant Health Survey, detect the selected visible dragon fruit diseases, estimate their severity according to the defined severity levels, group nearby diseased plants into proposed Disease Zones, and select the matching advisory recommendation from the expert-validated disease/severity catalogue. For Harvest Readiness Survey, analyse the defined visible maturity/readiness indicators and produce a pending Harvest Readiness assessment according to the output granularity established during domain study.

• Store AI outputs, boundary exceptions, proposed Disease Zones, selected treatment recommendations, and new/unmatched plant candidates as pending results associated with the relevant plants and survey until SystemManager review.

• For Follow-up Surveys, prepare comparison information using the most recent compatible verified observations of the same service.

• Notify responsible users when processing completes or review is required.

d) Non-Functional Requirements:

• Processing Performance: post-flight processing for a typical survey should complete within hours rather than days.

• Detection Accuracy: plant detection/counting must be validated against a manually counted reference plot; AI analysis must be evaluated against appropriate ground truth.

• Geospatial, Boundary and Re-identification Consistency: plant matching must combine positional information with row/column structure and be evaluated across repeated surveys. Farm assignment must respect an approved FarmBoundary, and outside/near-boundary detections must be flagged for review rather than silently assigned across Farms.

• Usability: web and mobile interfaces must use clear terminology and should not require customers to understand drone, GIS, or machine-learning details.

• Field Usability and Offline Support: the SystemManager mobile application should remain usable for essential field operations under weak connectivity, allowing necessary mission information and field notes to be cached locally and synchronized when connectivity is restored.

• Tenant Data Isolation and Access Control: TenantOwners access only their Tenant data; SystemManagers access only assigned operational data.

• Payment Integrity: for a Farm requiring Baseline Mapping, the no-separate-fee Baseline Mapping Mission may occur before payment to establish ConfirmedSurveyPoleCount. The selected paid Survey Service mission becomes eligible for execution only when its appointment has been confirmed and its required payment has been successfully confirmed. Price, confirmed pole-count, or scope changes after successful payment must follow an explicit auditable adjustment process rather than silent recalculation.

• Data Integrity and Auditability: approval/rejection, assignment, FarmBoundary verification, baseline pole-count confirmation, pricing confirmation, payment status, AI corrections, Disease Zone approval, treatment-recommendation provenance, owner-reported plant changes, lifecycle status changes, new-profile creation, and publication must be auditable; official results must distinguish AI output from human-verified output.

• Scalability and Media Storage: support a growing archive of drone imagery and per-plant records across repeated surveys and multiple Farms.

• Regulatory Compliance: field flights must follow applicable Vietnamese unmanned-aircraft requirements and document the compliance approach.

• Operational Safety: include pre-flight checks, unsuitable-flight conditions, and failsafe procedures for signal loss, low battery, or mission interruption.

e) Theory & Practical:

Theory

• Object Detection for Plant and Lesion Recognition using YOLO-family approaches.

• Multi-Object Tracking and De-duplication across overlapping video frames.

• Geospatial Data Modelling and Coordinate Transformation for FarmBoundary polygons, Zones, Plants, point-in-polygon/boundary checks, and map queries.

• Spatial Matching and Plant Re-identification under positional uncertainty using position plus regular row/column planting structure.

• Image Classification and Ordinal Severity Grading for ordered health severity levels.

• Spatial Clustering and Polygon Generation for grouping nearby verified/pending disease detections into proposed Disease Zones.

• Rule/Catalogue-Based Decision Support for mapping verified disease and severity outputs to expert-validated advisory treatment recommendations.

• Dragon Fruit Plant Pathology for selected diseases, including brown spot/stem canker and anthracnose, and confusing non-disease conditions such as sunburn and mechanical scarring.

• Precision Agriculture and Digital Plant Profiles for persistent per-plant records.

• Human-in-the-Loop Machine Learning as a quality gate before AI output becomes official.

Practical

• Assemble and flight-test a camera/GPS drone platform and establish repeatable mapping and inspection procedures.

• Collect real orchard imagery and build an annotated dataset for pole detection and selected disease/severity classes, including non-disease damage examples.

• Train/evaluate pole detection and tracking-based de-duplication against a manual count.

• Implement a mandatory Baseline Mapping workflow for Farms without an approved base map and confirmed inventory, including pole detection, tracking-based de-duplication, georeferencing, FarmBoundary checks, row/column derivation, SystemManager review, confirmed pole counting, and publication of the initial Farm base map and Digital Plant Profiles. Later surveys must reuse the approved base map and perform plant re-identification rather than rebuilding the Farm map.

• Implement and validate plant re-identification through repeated flights of the same zone.

• Train/evaluate disease detection and severity grading, explicitly analysing confusion with sunburn and mechanical damage; implement spatial grouping of nearby disease detections into proposed Disease Zones for SystemManager review.

• Implement the expert-validated disease/severity recommendation catalogue and deterministic AI-to-catalogue selection so published disease findings can include advisory treatment guidance without free-form treatment generation.

• Implement owner-reported plant inventory changes: TenantOwner submits removal/replacement/new-plant reports; SystemManager verifies lifecycle changes; old profiles remain historical/inactive; and later survey evidence plus SystemManager approval is required before creating a new Digital Plant Profile.

• Define and validate the visible Harvest Readiness criteria, output granularity, ground-truth procedure, dataset requirements, and evaluation protocol before evaluating the Harvest Readiness Survey capability.

• Build the backend API and spatial database for Survey Requests, Tenant onboarding and existing-Tenant New Farm registration, FarmBoundaries/Zones/Disease Zones, Survey Services, SurveyOrders, appointments, per-pole pricing/payments, drones, missions, Plants, owner-reported plant changes, inspections, treatment recommendations, AI review, published results, and persistent base-map reuse.

• Build the web platform for public requests, SystemAdmin, SystemManager, and TenantOwner.

• Build the mobile application for SystemManager field operations, including assigned surveys, appointments, survey scope/map, pre-flight checklist, mission status, field notes, and upload/synchronization support.

• Run an end-to-end field trial measuring counting deviation, disease accuracy, re-identification consistency, boundary-review cases, processing time, and SystemManager verification effort per 100 surveyed poles.

f) Products (Expected Deliverables):

• AgriDrone Web Platform: public service/request flow; SystemAdmin and SystemManager operations; TenantOwner portal for existing-Tenant New Farm registration, per-pole pricing/payment status, FarmBoundary and Disease Zone maps, Digital Plant Profiles, owner-reported plant changes, advisory treatment recommendations, published results, follow-up surveys, and history.

• AgriDrone Mobile Application: field-operation application for SystemManagers to access assigned surveys, appointments and survey scope/maps; complete pre-flight checks; record mission status and field notes; and upload/synchronize mission data in the field.

• Backend API and Spatial Database: ASP.NET Core Web API over PostgreSQL/PostGIS implementing Tenant/Farm onboarding, FarmBoundary validation, per-pole survey-service workflow, persistent Farm base maps, plant lifecycle/change reports, Disease Zones, recommendation catalogue, missions, plant profiles, review, and result domains.

• AI Processing Runtime: a .NET background worker/adapter in the same ASP.NET Core solution for plant detection/counting, tracking-based de-duplication, plant re-identification, boundary exception detection, disease detection, severity grading, proposed Disease Zone generation, expert-catalogue recommendation candidate selection, and service-specific analysis. The production runtime does not use a Python/FastAPI service; model artifacts are loaded through a .NET-compatible inference adapter and every output remains subject to the human-review gate.

• Operational Drone Platform: flight-tested drone with camera/GPS and documented mission procedures.

• Annotated Dragon Fruit Dataset: labelled field imagery for pole detection and selected disease/severity classes, including relevant non-disease damage.

• Field Trial and Evaluation Report: counting deviation, disease-analysis accuracy, re-identification consistency, boundary-review cases, processing time, and operational feasibility.

g) Proposed Tasks:

Team of 5 members, organized into six Work Packages. All members collaborate during field data collection, integration, testing, and the final field trial.

• WP1 – Project Management, Business Analysis & Drone Operations: requirements, survey-service workflow, domain/regulatory research, orchard coordination, mission planning, field operations, and documentation.

• WP2 – AI & Computer Vision: plant detection/counting, disease/severity, plant re-identification, boundary exception handling, Disease Zone generation, expert-catalogue recommendation selection, feasible service-specific AI analysis, AI service integration, and evaluation.

• WP3 – Core Backend & Business Domain: authentication, Tenant onboarding, existing-Tenant New Farm registration, FarmBoundaries/Zones/Disease Zones, Survey Services, Survey Requests, SurveyOrders, appointments, per-pole pricing/payment records, owner-reported plant changes, persistent Farm base maps, Plants, Digital Plant Profiles, recommendation catalogue, review, and publication.

• WP4 – Integration Backend, Media & Geospatial Services: mission/media ingestion, storage, asynchronous AI jobs, georeferencing, FarmBoundary and spatial validation, spatial queries/clustering support, notifications, and service integration.

• WP5 – Web Application: public request pages and SystemAdmin, SystemManager, and TenantOwner interfaces, including New Farm registration for existing Tenants, FarmBoundary/Disease Zone maps, Plant Profiles, owner-reported plant changes, treatment recommendations, per-pole payment/survey status, AI review, follow-up surveys, and history.

• WP6 – Mobile Application, QA & Field Validation: build the SystemManager mobile application for assigned surveys, appointments, survey scope/maps, pre-flight checklist, mission status, field notes, and upload/synchronization; lead end-to-end testing, field validation, stabilization, and final documentation.

3.3. Research Information

a. Research Problem / Research Question

The Digital Plant Profile depends on recognizing the same physical dragon fruit pole as the same record across repeated surveys. Consumer-grade drone GPS error may be comparable to the spacing between adjacent poles, so coordinate-only matching can confuse neighbouring plants and may also shift a detection toward or beyond the wrong Farm boundary. AgriDrone therefore uses an approved FarmBoundary as an operational guardrail: detections outside or suspiciously near the boundary are flagged for human review instead of being automatically assigned. RQ1 asks how consistently poles can be re-identified when detected position is combined with row/column index, compared with GPS-only matching and without RTK-grade positioning. RQ2 asks how accurately drone imagery can identify selected dragon fruit diseases and grade severity compared with expert on-site assessment, including the effect of confusion with sunburn and mechanical scarring. Disease Zone generation and expert-catalogue recommendations are system capabilities built on top of these findings rather than separate primary research questions.

Harvest Readiness is treated as an additional service capability rather than a primary scientific claim at registration time. Its observable criteria, ground-truth protocol, output granularity, and evaluation design must be established before it is promoted to a separate research question.

b. Research Objectives

• Construct an annotated real-orchard dataset for pole detection and selected disease/severity classes, including non-disease damage examples.

• Evaluate pole detection/tracking against a manual reference count.

• Measure plant re-identification correct-match and identity-switch rates across repeated surveys and compare grid-assisted matching with GPS-only matching.

• Evaluate disease detection using precision, recall, and F1-score and severity grading using an appropriate ordinal agreement measure against expert-labelled ground truth.

• Measure residual error caused by confusion between disease lesions and sunburn/mechanical damage.

• Assess operational feasibility using flight time, processing time, boundary-review cases, and SystemManager verification effort per 100 surveyed poles.

c. Research Scope & Methodology

A quantitative field study will be conducted on a real dragon fruit orchard in Vietnam, bounded to two to three visually distinguishable diseases and zones with a regular planting grid. A reference plot will be manually counted; selected plants will be inspected by an experienced grower or plant-protection specialist and corresponding imagery annotated. Models will be evaluated on held-out data. Re-identification will use repeat flights over the same zone and compare correct-match and identity-switch rates of the grid-assisted method against a GPS-only baseline on identical data. An approved FarmBoundary will be used as an operational guardrail, with outside/near-boundary detections recorded as review cases rather than automatically assigned. Disease detection and severity outputs will be compared with expert assessment, with confusion matrices used to isolate disease-versus-damage errors. Proposed Disease Zones will be generated from nearby disease detections and reviewed by the SystemManager, while treatment guidance will be selected only from the expert-validated disease/severity catalogue. Operational metrics will be collected throughout the field trial. Repeated surveys will demonstrate how verified observations are attached to persistent Digital Plant Profiles, how owner-reported removals/replacements are retained as lifecycle history, and how genuinely new plants receive new profiles only after later survey evidence and SystemManager verification.

d. Expected Scientific Contribution

An empirical method and measured benchmark for maintaining persistent per-plant digital identity in pole- or trellis-based orchards using low-cost drones without RTK positioning, enabling repeated observations to be associated with the same physical plants while using FarmBoundary review rules to reduce cross-Farm assignment risk. The project also produces an annotated dragon fruit disease dataset from real field flights, including visually confusing non-disease damage examples, with per-class accuracy and expert-agreement measurements for a Vietnamese production setting. On top of the verified disease outputs, the system demonstrates human-reviewed Disease Zone summarization and catalogue-based advisory recommendations as practical decision-support capabilities.

e. Related Works / Literature Review (Preliminary)

• UAV-based individual plant/tree detection and counting in orchards.

• Deep-learning crop disease detection and severity grading, particularly YOLO-family detectors.

• Multi-object tracking methods such as SORT, DeepSORT, and ByteTrack.

• Consumer-grade GNSS versus RTK/PPK for plant-level georeferencing.

• Spatial clustering, farm-boundary/point-in-polygon validation, and human review of uncertain geospatial detections.

• Dragon fruit pathology literature on brown spot/stem canker, anthracnose, sunburn, and mechanical damage.

• Digital-twin/per-plant records and human-in-the-loop validation in precision agriculture.

4. Other comments (propose all relative things if have):

• A Farm without an approved base map and confirmed active-pole inventory must first complete a mandatory Baseline Mapping Mission. This mission is not sold or charged as a separate Survey Service; it establishes the Farm base map and ConfirmedSurveyPoleCount used for per-pole pricing. The selected paid Survey Service mission is executed only after the confirmed count, final price, appointment, and payment are completed. The approved base map and Digital Plant Profiles are then reused for later services and follow-up surveys.

• Every Farm uses an approved FarmBoundary polygon. Detections outside the boundary or suspiciously near it are not automatically assigned to the Farm and must be flagged OutOfBoundary/NeedsReview for SystemManager verification.

• AI may propose Disease Zones by clustering nearby diseased plants, but a SystemManager must review/correct and approve each zone before TenantOwner publication. Disease treatment guidance must be selected from an expert-validated disease/severity catalogue and remains advisory only.

• TenantOwner is responsible for reporting removed/replaced or newly added plants. Reports are pending until SystemManager verification. Historical Digital Plant Profiles are retained and marked inactive/removed; a new plant receives a new profile only after it is observed in a later survey and verified.

• Plant re-identification remains the technical core and must be measured explicitly across repeat flights.

• Drone assembly, flight stability, and flight authorization are schedule risks; a fallback capture device should be considered.

• Keep the disease class set small and explicitly handle sunburn/mechanical scarring as confusing non-disease conditions.

• Harvest Readiness must not be presented as validated until its criteria, dataset, ground truth, output granularity, and evaluation method are fixed. It estimates readiness from visible imagery only; harvest quantities, yield tracking, harvest batches, and post-harvest management remain out of scope.

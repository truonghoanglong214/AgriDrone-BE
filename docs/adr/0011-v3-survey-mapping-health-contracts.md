# ADR-0011: V3 Survey, Mapping và Plant Health contracts

- Status: Accepted
- Date: 2026-10-07
- Decision owners: Backend 1, Backend 2

## Context

V2 contracts thiếu purpose-aware readiness, approved boundary version, boundary exceptions, confirmed pole count, Disease Zones, recommendation candidates và plant-change evidence. Thay đổi V2 tại chỗ sẽ phá consumer và stored Inbox/Outbox/DLQ payload.

## Decision

- V1 và V2 giữ immutable. V3 dùng type, event name, schema version, consumer name và Inbox identity riêng.
- `SurveyOrderOperationalContextV3` là synchronous/internal query contract, không phải integration event.
- Ba V3 integration events được phát hành:
  - `mapping.baseline-candidates-ready.v3` (`BaselineMappingCandidatesReadyV3`, BE2 → BE1);
  - `mapping.farm-base-map-published.v3` (`FarmBaseMapPublishedV3`, BE1 → BE2);
  - `health.analysis-ready.v3` (`PlantHealthAnalysisReadyV3`, BE2 → BE1).
- `BaselineMappingCandidatesReadyV3` mang pending candidates; tên và semantics không được nói rằng candidates đã được approved.
- Geometry dùng RFC 7946 GeoJSON/WGS84. Contract không tham chiếu EF Core hoặc NetTopologySuite types.
- Stable code/state values serialize thành strings; consumer không phụ thuộc numeric enum ordinal.
- Event dùng envelope hiện hữu. Payload mang `CausationId`, handoff/publication ID, SurveyOrderId, MissionId, FarmId, FarmBoundaryVersionId, FarmBaseMapVersionId khi áp dụng, source job/model/policy versions và UTC business timestamp.
- MVP giữ payload inline, tối đa 10.000 items và 4 MiB. Payload vượt giới hạn bị từ chối bằng stable validation error và producer phải chia scope/job; V3 không thêm multipart hoặc manifest URI.
- Mỗi version có validator, golden JSON, round-trip và version-routing tests độc lập.
- Producer/consumer cutover theo producer switch, queue/DLQ drain, retention và replay gate; không xóa/rewrite stored V2 messages.

## Consequences

ADR-0005 tiếp tục đúng cho nguyên tắc immutable version evolution; ADR này áp dụng nguyên tắc đó cho V3. Field-level specification nằm tại `docs/domain/be1-step0r-locked-contract.md`.

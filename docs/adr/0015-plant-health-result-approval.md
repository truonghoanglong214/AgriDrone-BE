# ADR 0015 — Approve an exact reviewed Plant Health candidate

> **V3 scope notice — 2026-11-10.**
> Historical ADR: accepted/superseded status below records its original checkpoint. ADR 0018 partially supersedes service, funding, publication, comparison and architecture semantics; unchanged isolation, history and immutable wire guarantees remain applicable.
> Current: [Business Rules v3](../../Codex-Plan/Business-Rules-v3.md), [ADR 0018](0018-v3-paid-map-combo-business-policy.md), [target contracts](../contracts/survey-business-v3.md). Documentation-only update; no runtime change.

- Date: 2026-11-09
- Status: Accepted for implementation; runtime acceptance deferred to Final Test Phase
- Scope: Step 7C / UC09, following [ADR 0014](0014-plant-health-human-review.md)

## Decisions

1. Approval is a separate manager mutation, not a derived status sent by the client.
   Input contains OperationId, active BatchId, expected result xmin/review revision
   and reason. Actor/Tenant/time/result nature are resolved on the server.
2. Reuse the 7B application port, execution context and current primary Manager
   guard. One transaction locks Order → Result, authorization, review heads, zone/
   Plant associations, catalogue/guidance, source Mission/job, capture map/boundary
   and referenced media. Review/reprocess cannot change the candidate through
   the normal application paths during approval.
3. Recompute live blockers; never accept client ReadyForApproval or ingestion
   BlockingCount. All required finding/exception/zone decisions must be final,
   member/guidance bindings current, configured classes valid, evidence owned by
   source context and provenance complete. Validate geometry again in PostGIS,
   using set-based point/zone checks instead of one query per finding.
4. Revalidate source payload identity/fingerprint and original item set against the
   immutable handoff. Source job must be completed for the same Plant Health
   Order/Tenant/Farm/capture versions/provenance/limitation. It must retain actual
   registered input media and a positive processed-image count. Zero images is
   not a valid zero-finding survey.
5. Empty findings or all Reject/NonDisease decisions may be approved after those
   source/evidence gates and explicit manager approval reason. Conclusion is
   NO_ACCEPTED_SUSPECTED_FINDINGS, never HEALTHY or an expert-confirmed absence of
   disease. Other candidates use HAS_REVIEWED_SUSPECTED_FINDINGS. Result nature
   remains PRELIMINARY_SUSPECTED with the original team-label limitation.
6. Store explicit ApprovedBatchId/ApprovedReviewRevision, immutable approval snapshot
   and SHA-256 fingerprint on SurveyResult. Snapshot records required review IDs,
   subjects/kinds/decisions, head versions and immutable reviewed-value fingerprints,
   source/map/job/provenance/evidence identities, actor/time/reason and conclusion.
   Guidance decisions for rejected zones are not part of the required publication set.
7. Result-level Provenance is aligned to the active approved batch; older source
   provenance remains immutable on its import batches. The generic legacy Approve
   method cannot approve V4 without this reviewed-candidate snapshot.
8. Persist result Approved + metadata + approval audit + operation receipt atomically.
   Receipt uses the existing actor/Tenant/OperationId namespace and distinct action
   fingerprint. Replay reauthorizes and returns the original approval outcome;
   CurrentStatus separately reflects later publication. Different content or a new
   operation attempting to approve an already locked result returns conflict.
9. Database checks/FK bind the approved batch to the same result/Tenant/Farm and
   require complete V4 approval metadata. SQL conditions explicitly handle nullable
   legacy result nature/active batch; NULL must not bypass the new checks.
10. Once approved, DB trigger prevents replacing approval evidence, review batch/
    revision, reviewer/time, source context, nature/limitation/provenance or reopening
    the result. Status may progress to Published while keeping approval evidence.
    Down is guarded when approved evidence exists; no fake backfill is allowed.
11. Order remains PendingReview. No zone publication, latest-reviewed update,
    completion event, owner portal/notification or Mission completion is performed.
    Manager-only GET approval checks the stored snapshot fingerprint before returning metadata.

## Handoff to 7D

7D must reload/revalidate current assignment, approval fingerprint/review references,
capture associations and catalogue/guidance effective windows before publication.
Approved is not owner-visible. If dependencies become invalid after approval,
publication must fail closed; reopening/revision requires an explicit workflow,
not mutation of the approved snapshot.

Source context adds optional internal InputMediaIds/ProcessedImageCount derived
from actual job manifests; the immutable V4 wire contract/routing stays unchanged.
Existing jobs without those manifests require reprocess, not invented provenance.

No tests are written/run in 7C. Verification is production build + EF model checks;
acceptance/concurrency/rollback/security/migration tests remain in Final Test Phase.

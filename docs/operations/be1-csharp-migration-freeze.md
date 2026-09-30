# BE1 C# migration freeze

Effective: 2026-09-28, after the Migration Phase 0 baseline was captured.

Active BE1 C# code is frozen. Identity, Farm, official Plant/catalogue, Survey, BE1 Notifications, BE1 messaging/audit/cache and BE1-owned API surfaces accept no new product feature. New BE1 capability is implemented only in `backend-be1-java` after its corresponding migration gate is open.

Allowed C# changes are limited to:

1. a defect that prevents an accepted parity case from running;
2. a security or data-integrity fix required during the compatibility window;
3. a contract adapter needed to remove a BE2 compile-time/database dependency;
4. instrumentation or a reversible route/consumer switch required for cutover;
5. removal work explicitly authorized by Migration Phase 10/11.

Every exception must be added to the table before merge and must include the affected capability, reason, approver, issue/PR, parity impact and removal phase. An exception may preserve or repair frozen behavior; it may not introduce new BE1 business scope.

| Date | Capability | Reason | Approver | Issue/PR | Parity impact | Removal phase | Status |
|---|---|---|---|---|---|---|---|
| — | — | No active exception at Phase 0 close | — | — | — | — | Closed |

BE2-owned Mission/Drone/media/telemetry/AI work is not frozen by this file, but it may not add a direct reference, SQL query, shared EF model or foreign key to BE1-owned data.

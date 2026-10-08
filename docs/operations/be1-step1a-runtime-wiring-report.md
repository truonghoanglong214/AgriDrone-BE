# BE1 Step 1A — Surveys runtime wiring report

- Date: 2026-10-08
- Scope: Step 1A runtime foundation
- Status: **CodeComplete — AwaitingFinalTest**
- Public Survey feature API opened: no

## Delivered

- Added the direct API project reference to `AgriDrone.Modules.Surveys` and one
  `AddSurveysModule` call in the application composition root.
- Added the module composition root with scoped `SurveysDbContext`,
  `ISurveysUnitOfWork`, `ISurveyServiceRepository` and
  `ISurveyCatalogueQueries` registrations.
- Registered all PostgreSQL survey enums, MediatR handlers, FluentValidation
  validators and `TimeProvider` without introducing a dependency on
  `AgriDrone.Database`.
- Extended `SurveysDbContext` with module transaction behavior, audit and Outbox
  persistence. `AgriDroneSchemaDbContext` remains the only migration owner.
- Added the tracked SurveyService mutation repository and no-tracking catalogue,
  price-history, effective-price and overlap query projections. EF entities are
  not exposed through the Application boundary.
- Added the `surveys-database` readiness check. It resolves the scoped Surveys
  context and verifies database connectivity through EF Core; `/health/ready`
  includes it through the existing `ready` tag.

## Boundary decisions

- Step 1A does not add controllers or expose the catalogue routes. Public and
  SystemAdmin catalogue behavior remains owned by Steps 1B–1D.
- SurveyRequest/SurveyOrder/payment/result repositories are not created early;
  their use-case-specific contracts remain owned by later steps.
- No schema migration is required. Audit/Outbox and all Survey tables already
  exist in the canonical schema; this checkpoint wires a runtime module context
  over them.

## Verification

- Full solution build: succeeded with zero warnings and zero errors.
- Runtime wiring smoke check used `ValidateOnBuild = true` and
  `ValidateScopes = true`.
- UoW, repository and catalogue query services resolved from a scoped provider.
- The health-check registration resolved successfully.
- The Surveys EF model constructed successfully with 13 entities.

Architecture, database-health integration and feature behavior tests remain in
the unified Final Test Phase. Step 1A is therefore code-complete, not `Done`.

# BE1–BE2 golden event fixtures

These files lock the language-neutral JSON wire shape at Java migration Phase 0. They are canonical examples, not production messages: identifiers and tokens are deterministic test values.

Compatibility rules:

- filenames ending in `.event.json` contain one complete integration envelope;
- property names, string enum values, decimal representation, UUIDs and UTC offsets are part of the fixture;
- V1 fixtures are immutable during the compatibility window;
- V2 changes require a new schema version/event type rather than editing an established fixture;
- both BE1 and BE2 contract tests must read these files; neither side may generate its expectation from a language-specific DTO.

The direction and ownership of each event are recorded in `docs/operations/be1-java-migration-phase0-report.md`.

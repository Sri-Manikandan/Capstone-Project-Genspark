# Frontend Production Readiness — Design

**Date:** 2026-06-29
**Scope:** `EMSAngular/` (Angular 22 frontend)
**Goal:** Bring the entire Angular frontend to a production-shippable state in a single sweep.

## Context

The frontend builds cleanly today (prod build: ~280 kB main, lazy-loaded routes, budgets configured) and the code is largely clean (no stray `console.log`s, no TODO/FIXME, `any` confined to specs). However a readiness audit surfaced two hard blockers plus several verification/polish gaps. A large mobile-responsiveness refactor (~111 changed files, templates/styles extracted to `.html`/`.css`) is in progress and currently uncommitted.

## Audit Findings

| # | Severity | Finding |
|---|----------|---------|
| 1 | 🔴 Blocker | `angular.json` has **no `fileReplacements`**. Every service imports `environments/environment`, so the production build silently ships `environment.ts` (`localhost:5222` + a `pk_test` Stripe key). `environment.prod.ts` is dead code. |
| 2 | 🔴 Blocker | **Test suite does not compile.** A recently-added required `screen` field on `EventDto` was not propagated to `event-form.component.spec.ts` and `event-card.component.spec.ts` (and possibly other fixtures). `ng test` is red. |
| 3 | 🟡 Verify | The ~111-file responsive refactor compiles & builds but has not been verified for consistency/completeness across every view. |
| 4 | 🟡 Polish | `environment.prod.ts` holds placeholder values; no documentation tells a deployer what to fill in. |
| 5 | 🟡 Polish | Loading / empty / error UX states and accessibility (labels, contrast, keyboard) have not been audited for consistency. |

## Decisions (from brainstorming)

- **Prod secrets:** Wire `fileReplacements` correctly but keep clearly-marked **placeholders** in `environment.prod.ts`; document them in `EMSAngular/README.md`. Do not bake real values.
- **Depth:** Full sweep — all four workstreams in one pass, ending with build + tests green and the app manually verified.

## Workstreams

### W1 — Config hardening (blocker)
- Add a `fileReplacements` entry to the `production` build configuration in `angular.json` mapping `src/environments/environment.ts` → `src/environments/environment.prod.ts`.
- Keep `environment.prod.ts` values as explicit, clearly-labelled placeholders (`apiBaseUrl`, `stripePublishableKey`).
- Document required deploy-time env values and how to set them in `EMSAngular/README.md`.
- **Done when:** a production build resolves the prod environment file (verified), and the README explains the placeholders.

### W2 — Green the test suite (blocker)
- Fix `EventDto`-fixture compile breaks (`screen` field) wherever they occur; grep all specs for stale `EventDto`/model fixtures, not just the two known files.
- Run the full suite; fix any other failing/broken specs.
- Note (not necessarily fill) coverage gaps for any production-critical path that has zero tests.
- **Done when:** `ng test --watch=false` compiles and passes.

### W3 — Verify / finish responsive refactor
- Sweep all 31 components for refactor consistency (every component that lost its inline template has a matching `.html`/`.css`; no orphaned inline templates; consistent breakpoint usage per the mobile-responsiveness design doc).
- Run the app and spot-check key flows at mobile + desktop widths (home, event list/detail, checkout, bookings, admin, organizer).
- **Done when:** refactor is internally consistent and the app renders correctly at representative breakpoints.

### W4 — Error/UX states + accessibility polish
- Audit for consistent **loading**, **empty**, and **error** states across data-driven views; ensure API failures surface a user-facing message (via the shared alert) rather than a blank/broken view.
- Accessibility pass: form-control labels, image `alt` text, button/icon `aria-label`s, focus visibility, obvious contrast issues.
- Keep changes incremental and aligned with existing shared components (`alert`, `loading-spinner`, `modal`).
- **Done when:** data views degrade gracefully on error/empty and the obvious a11y gaps are closed.

## Sequencing

W1 → W2 (blockers, fast, unblock a trustworthy build/test signal) → W3 → W4. Each workstream is independently verifiable. The pass ends with: prod build passes, `ng test` green, app manually verified, changes committed (commit messages ≤5 words per CLAUDE.md).

## Out of Scope

- Backend (`EventManagementSystem/`) changes.
- New features or redesigns beyond the in-progress responsive work.
- Real production secret values (placeholders only).
- E2E test infrastructure (none exists today; not introducing it this pass).

## Risks

- The responsive refactor is large and uncommitted; W3 verification may surface rework. Mitigation: verify against the existing `2026-06-26-mobile-responsiveness-design.md` rather than redesigning.
- W4 is open-ended; bound it to *consistency and obvious gaps*, not an exhaustive WCAG audit.

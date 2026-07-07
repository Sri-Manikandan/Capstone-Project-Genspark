# Profile Page — Validation & Error Handling

**Date:** 2026-07-07
**Area:** `EMSAngular/src/app/features/profile/`

## Problem

The profile page hosts five independent forms — organizer request, change
password, edit profile, change email, and close account — but its validation and
error handling are incomplete and inconsistent:

- **Weak validators.** The client-side rules do not match the backend
  `InputValidator`. Phone has no pattern, name has no max length, and the new
  password only checks length while the backend also requires upper/lower/digit/
  special characters.
- **Inconsistent inline errors.** Only the password fields show ad-hoc `<span>`
  messages. The rest of the fields show nothing, even though the app already has
  a reusable `ems-field-error` component used by the register form.
- **One shared alert for five forms.** A single top-of-page `error`/`success`
  signal serves every form. A failure on "Close account" (bottom of a long page)
  surfaces its message at the very top, far from where the user is looking. Stale
  messages from a previous action also linger because nothing clears them.

## Goals

1. Align every profile form's client-side validators with the backend
   `InputValidator` rules.
2. Show a per-field validation message under every input, using the existing
   `ems-field-error` component.
3. Scope error/success feedback to each form so the message renders next to the
   action, and clear it before each new attempt.

## Non-goals

- No changes to `UserService`, the API, or the backend DTOs. Server messages
  already flow through `extractError`.
- No redesign of the page layout or styling beyond adding error/hint elements.
- No change to the submit-on-invalid behaviour (forms still `markAllAsTouched()`
  on an invalid submit rather than disabling the button on invalid).

## Validation rules

Mirrors `EMSBLLLibrary/Helpers/InputValidator.cs`.

| Field | Validators |
|---|---|
| Profile · Name | `required`, `minLength(2)`, `maxLength(100)` |
| Profile · Phone | `required`, `pattern(/^\+?[0-9]{7,15}$/)` |
| Email · New email | `required`, `email` |
| Email · Password | `required` |
| Password · Current | `required` |
| Password · New | `required`, `minLength(8)`, `passwordComplexity` |
| Password · Confirm | `required`; group validator `passwordsMatch` (unchanged) |
| Request · Reason | `required`, `minLength(10)` |
| Close · Password | `required` |

`passwordComplexity` is a new custom validator (local to the component) that
returns `{ complexity: true }` when the value is non-empty and missing any of:
an uppercase letter, a lowercase letter, a digit, or a special character
(`[^a-zA-Z0-9]`). A static hint line renders under the new-password field
describing the requirement.

## Error / success handling

Replace the single `error`/`success` pair with per-form signals:

- `loadError` — page-level; set when initial `getMe()` or the organizer-request
  load fails. Rendered by the existing top alert.
- `requestError` / `requestSuccess`
- `passwordError` / `passwordSuccess`
- `profileError` / `profileSuccess`
- `emailError` / `emailSuccess`
- `closeError`

Each handler:

1. On invalid form → `markAllAsTouched()` and return (so `ems-field-error`
   messages appear).
2. Before the service call → clear that form's own error and success signals.
3. On error → set that form's error signal.
4. On success → set that form's success signal (where one exists today).

Each `<section>` renders its own `<ems-alert type="error">` and, where
applicable, `<ems-alert type="success">` bound to its signals.

## Template changes

- Import `FieldErrorComponent` into the profile component.
- Add `<ems-field-error [control]="…" label="…">` under every input listed above.
- Keep the group-level "Passwords don't match" span (the `mismatch` error lives
  on the form group, not a single control), and drop the redundant ad-hoc
  min-length span now covered by `ems-field-error`.
- Add the password-complexity hint line under the new-password field.
- Move the shared top alert to bind `loadError`; add per-form alerts inside each
  section.

## Testing

Extend `profile.component.spec.ts`:

- Invalid phone (e.g. `"abc"`), too-short name, and a non-complex password each
  keep their form invalid and block the service call.
- A failed request sets that form's error signal; a subsequent attempt clears it
  first.
- A successful action sets that form's success signal and clears its error.

## Files touched

- `EMSAngular/src/app/features/profile/profile.component.ts`
- `EMSAngular/src/app/features/profile/profile.component.html`
- `EMSAngular/src/app/features/profile/profile.component.spec.ts`

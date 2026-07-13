import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { istNowMs } from '../date/ist-now';

/**
 * Reusable reactive-form validators that mirror the backend `InputValidator`
 * and event/venue service rules, so the client rejects the same input the API would.
 *
 * Datetime values are IST wall-clock strings, which is how the API reads them, so
 * anything compared against "now" uses `istNowMs()` rather than the browser's clock.
 */

/** Requires an absolute http(s) URL. Mirrors `InputValidator.ValidateUrl`. */
export function httpUrl(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '').toString().trim();
  if (!value) return null; // let `required` own the empty case
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return { url: true };
  }
  return url.protocol === 'http:' || url.protocol === 'https:' ? null : { url: true };
}

/** Requires a datetime strictly in the future. Mirrors the create-event StartTime check. */
export function futureDateTime(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '').toString().trim();
  if (!value) return null;
  const when = new Date(value).getTime();
  if (Number.isNaN(when)) return null;
  return when > istNowMs() ? null : { notFuture: true };
}

/**
 * Requires a datetime at least `hours` in the future. Mirrors the backend create/edit
 * lead-time rule (events must be scheduled ≥ 48 hours ahead). Surfaces
 * `{ minLeadTime: { hours } }` so the field error can report the required window.
 */
export function minLeadTime(hours: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = (control.value ?? '').toString().trim();
    if (!value) return null; // let `required` own the empty case
    const when = new Date(value).getTime();
    if (Number.isNaN(when)) return null;
    return when >= istNowMs() + hours * 3600_000 ? null : { minLeadTime: { hours } };
  };
}

/**
 * Rejects a value that is present but only whitespace. Mirrors the backend's
 * `string.IsNullOrWhiteSpace` checks. Returns null for an empty value so
 * `Validators.required` owns that case. Surfaces `{ notBlank: true }`.
 */
export function notBlank(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '').toString();
  if (value.length === 0) return null; // let `required` own the empty case
  return value.trim().length === 0 ? { notBlank: true } : null;
}

/**
 * Requires a datetime no earlier than a bound supplied by `boundFn` (a wall-clock
 * "YYYY-MM-DDTHH:mm" string, or null/undefined while it is still loading). Used to keep a
 * value inside a window whose edge is fetched asynchronously. Surfaces `{ [key]: true }`.
 */
export function notBefore(boundFn: () => string | null | undefined, key: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = (control.value ?? '').toString().trim();
    const bound = (boundFn() ?? '').toString().trim();
    if (!value || !bound) return null;
    const when = new Date(value).getTime();
    const limit = new Date(bound).getTime();
    if (Number.isNaN(when) || Number.isNaN(limit)) return null;
    return when >= limit ? null : { [key]: true };
  };
}

/**
 * Requires a datetime no later than a bound supplied by `boundFn` (a wall-clock
 * "YYYY-MM-DDTHH:mm" string, or null/undefined while it is still loading). Mirror of
 * `notBefore` for an upper edge. Surfaces `{ [key]: true }`.
 */
export function notAfter(boundFn: () => string | null | undefined, key: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = (control.value ?? '').toString().trim();
    const bound = (boundFn() ?? '').toString().trim();
    if (!value || !bound) return null;
    const when = new Date(value).getTime();
    const limit = new Date(bound).getTime();
    if (Number.isNaN(when) || Number.isNaN(limit)) return null;
    return when <= limit ? null : { [key]: true };
  };
}

/** Requires a numeric select to hold a positive id (0 is the placeholder option). */
export function selectRequired(control: AbstractControl): ValidationErrors | null {
  return Number(control.value) > 0 ? null : { required: true };
}

/**
 * Group-level validator: the control at `endKey` must be after the one at `startKey`.
 * Surfaces `{ endBeforeStart: true }` on the end control so a field error renders inline.
 */
export function endAfterStart(startKey: string, endKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = group.get(startKey)?.value;
    const endControl = group.get(endKey);
    const end = endControl?.value;
    if (!start || !end || !endControl) return null;

    const startMs = new Date(start).getTime();
    const endMs = new Date(end).getTime();
    if (Number.isNaN(startMs) || Number.isNaN(endMs)) return null;

    const existing = endControl.errors ?? {};
    if (endMs > startMs) {
      if (existing['endBeforeStart']) {
        const { endBeforeStart: _removed, ...rest } = existing;
        endControl.setErrors(Object.keys(rest).length ? rest : null);
      }
      return null;
    }

    endControl.setErrors({ ...existing, endBeforeStart: true });
    return { endBeforeStart: true };
  };
}

/**
 * The API speaks IST wall-clock times with no offset ("2026-07-20T18:30"), and
 * `datetime-local` inputs hold the same shape. Parsing one with `new Date(...)`
 * therefore reads it in the *browser's* zone, so comparing it against `Date.now()`
 * is only correct for a browser already in IST.
 *
 * These helpers give the current moment expressed on the IST clock, so form
 * validation and prefilled times agree with the server no matter where the user is.
 */

const IST_OFFSET_MS = 5.5 * 60 * 60 * 1000;

/**
 * "Now" in IST, as milliseconds on the same scale a wall-clock string lands on when
 * parsed with `new Date(value)`. Compare the two directly:
 *
 *   new Date(control.value).getTime() >= istNowMs()
 *
 * In an IST browser this is exactly `Date.now()`; elsewhere it shifts by the
 * difference between the local zone and IST.
 */
export function istNowMs(): number {
  const now = new Date();
  return now.getTime() + IST_OFFSET_MS + now.getTimezoneOffset() * 60_000;
}

/** "Now" on the IST clock as a `datetime-local` string ("YYYY-MM-DDTHH:mm"). */
export function istNowWallClock(): string {
  const ist = new Date(istNowMs());
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${ist.getFullYear()}-${pad(ist.getMonth() + 1)}-${pad(ist.getDate())}` +
    `T${pad(ist.getHours())}:${pad(ist.getMinutes())}`;
}

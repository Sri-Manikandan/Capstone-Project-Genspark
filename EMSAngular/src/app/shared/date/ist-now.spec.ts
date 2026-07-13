import { istNowMs, istNowWallClock } from './ist-now';

/** The current IST wall clock, read straight from Intl — ground truth, independent of the host zone. */
const istViaIntl = () =>
  new Intl.DateTimeFormat('sv-SE', {
    timeZone: 'Asia/Kolkata',
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(new Date()).replace(' ', 'T');

/** Compare two wall-clock strings by their digits alone, both read as UTC. */
const wallClockMs = (s: string) => new Date(`${s}Z`).getTime();

describe('istNowWallClock', () => {
  it('reports the IST wall clock regardless of the browser timezone', () => {
    // Both readings truncate to the minute, so allow one tick in case the minute
    // rolls over between the two calls.
    const drift = Math.abs(wallClockMs(istNowWallClock()) - wallClockMs(istViaIntl()));
    expect(drift).toBeLessThanOrEqual(60_000);
  });
});

describe('istNowMs', () => {
  it('is the scale a wall-clock string lands on when parsed as a local Date', () => {
    // `new Date(W)` reads W in the host zone, so an IST wall-clock string parsed that
    // way must line up with istNowMs() — this is what lets the validators compare them.
    // The string is minute-truncated, so it trails istNowMs() by under a minute.
    const behind = istNowMs() - new Date(istNowWallClock()).getTime();
    expect(behind).toBeGreaterThanOrEqual(0);
    expect(behind).toBeLessThan(60_000);
  });

  it('orders IST wall-clock strings around "now" correctly', () => {
    const wall = (ms: number) => {
      const d = new Date(istNowMs() + ms);
      const pad = (n: number) => String(n).padStart(2, '0');
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };
    expect(new Date(wall(60 * 60_000)).getTime()).toBeGreaterThan(istNowMs());
    expect(new Date(wall(-60 * 60_000)).getTime()).toBeLessThan(istNowMs());
  });
});

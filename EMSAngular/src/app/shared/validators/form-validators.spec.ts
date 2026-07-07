import { FormControl, FormGroup } from '@angular/forms';
import { endAfterStart, futureDateTime, httpUrl, minLeadTime, notAfter, notBefore, notBlank, selectRequired } from './form-validators';

describe('httpUrl', () => {
  it('passes for absolute http and https URLs', () => {
    expect(httpUrl(new FormControl('http://example.com/a.png'))).toBeNull();
    expect(httpUrl(new FormControl('https://example.com'))).toBeNull();
  });

  it('ignores empty values (required owns that case)', () => {
    expect(httpUrl(new FormControl(''))).toBeNull();
  });

  it('fails for non-http schemes and malformed URLs', () => {
    expect(httpUrl(new FormControl('ftp://example.com'))).toEqual({ url: true });
    expect(httpUrl(new FormControl('not a url'))).toEqual({ url: true });
    expect(httpUrl(new FormControl('example.com'))).toEqual({ url: true });
  });
});

describe('futureDateTime', () => {
  // datetime-local inputs emit a local wall-clock string ("YYYY-MM-DDTHH:mm"), so build
  // the test values in local time rather than UTC to match how the validator parses them.
  const localOffset = (ms: number) => {
    const d = new Date(Date.now() + ms);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  };

  it('passes for a datetime in the future', () => {
    expect(futureDateTime(new FormControl(localOffset(60 * 60_000)))).toBeNull();
  });

  it('fails for a datetime in the past', () => {
    expect(futureDateTime(new FormControl(localOffset(-60 * 60_000)))).toEqual({ notFuture: true });
  });

  it('ignores empty values', () => {
    expect(futureDateTime(new FormControl(''))).toBeNull();
  });
});

describe('minLeadTime', () => {
  // datetime-local emits local wall-clock strings; build test values in local time.
  const localOffset = (ms: number) => {
    const d = new Date(Date.now() + ms);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  };

  it('passes for a datetime at least the required hours ahead', () => {
    expect(minLeadTime(48)(new FormControl(localOffset(49 * 60 * 60_000)))).toBeNull();
  });

  it('fails for a datetime within the lead-time window', () => {
    expect(minLeadTime(48)(new FormControl(localOffset(24 * 60 * 60_000)))).toEqual({ minLeadTime: { hours: 48 } });
  });

  it('fails for a datetime in the past', () => {
    expect(minLeadTime(48)(new FormControl(localOffset(-60 * 60_000)))).toEqual({ minLeadTime: { hours: 48 } });
  });

  it('ignores empty values (required owns that case)', () => {
    expect(minLeadTime(48)(new FormControl(''))).toBeNull();
  });
});

describe('notBlank', () => {
  it('ignores an empty value (required owns that case)', () => {
    expect(notBlank(new FormControl(''))).toBeNull();
  });

  it('fails for a whitespace-only value', () => {
    expect(notBlank(new FormControl('   '))).toEqual({ notBlank: true });
  });

  it('passes for a value with non-whitespace content', () => {
    expect(notBlank(new FormControl('  Main Hall  '))).toBeNull();
  });
});

describe('notBefore', () => {
  it('fails when the value is earlier than the bound', () => {
    const control = new FormControl('2026-07-10T18:00');
    expect(notBefore(() => '2026-07-10T20:00', 'outsideWindow')(control)).toEqual({ outsideWindow: true });
  });

  it('passes when the value is at or after the bound', () => {
    expect(notBefore(() => '2026-07-10T20:00', 'outsideWindow')(new FormControl('2026-07-10T20:00'))).toBeNull();
    expect(notBefore(() => '2026-07-10T20:00', 'outsideWindow')(new FormControl('2026-07-10T21:00'))).toBeNull();
  });

  it('ignores the check when the bound is not yet available', () => {
    expect(notBefore(() => null, 'outsideWindow')(new FormControl('2026-07-10T18:00'))).toBeNull();
  });

  it('ignores an empty control value', () => {
    expect(notBefore(() => '2026-07-10T20:00', 'outsideWindow')(new FormControl(''))).toBeNull();
  });
});

describe('notAfter', () => {
  it('fails when the value is later than the bound', () => {
    const control = new FormControl('2026-07-10T22:00');
    expect(notAfter(() => '2026-07-10T20:00', 'outsideWindow')(control)).toEqual({ outsideWindow: true });
  });

  it('passes when the value is at or before the bound', () => {
    expect(notAfter(() => '2026-07-10T20:00', 'outsideWindow')(new FormControl('2026-07-10T20:00'))).toBeNull();
    expect(notAfter(() => '2026-07-10T20:00', 'outsideWindow')(new FormControl('2026-07-10T19:00'))).toBeNull();
  });

  it('ignores the check when the bound is not yet available', () => {
    expect(notAfter(() => undefined, 'outsideWindow')(new FormControl('2026-07-10T22:00'))).toBeNull();
  });

  it('ignores an empty control value', () => {
    expect(notAfter(() => '2026-07-10T20:00', 'outsideWindow')(new FormControl(''))).toBeNull();
  });
});

describe('selectRequired', () => {
  it('fails when the select still holds the 0 placeholder', () => {
    expect(selectRequired(new FormControl(0))).toEqual({ required: true });
  });

  it('passes for a positive id', () => {
    expect(selectRequired(new FormControl(5))).toBeNull();
  });
});

describe('endAfterStart', () => {
  const build = (start: string, end: string) =>
    new FormGroup(
      { startTime: new FormControl(start), endTime: new FormControl(end) },
      { validators: endAfterStart('startTime', 'endTime') },
    );

  it('marks the end control invalid when it is not after the start', () => {
    const group = build('2026-07-10T20:00', '2026-07-10T19:00');
    expect(group.errors).toEqual({ endBeforeStart: true });
    expect(group.get('endTime')?.errors).toEqual({ endBeforeStart: true });
  });

  it('is valid when the end is after the start', () => {
    const group = build('2026-07-10T20:00', '2026-07-10T22:00');
    expect(group.errors).toBeNull();
    expect(group.get('endTime')?.errors).toBeNull();
  });

  it('clears its own error once the times become valid', () => {
    const group = build('2026-07-10T20:00', '2026-07-10T19:00');
    expect(group.get('endTime')?.errors).toEqual({ endBeforeStart: true });
    group.get('endTime')?.setValue('2026-07-10T23:00');
    expect(group.get('endTime')?.errors).toBeNull();
  });
});

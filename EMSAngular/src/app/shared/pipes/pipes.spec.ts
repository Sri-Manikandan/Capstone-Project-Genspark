import { IstDatePipe } from './ist-date.pipe';
import { CurrencyInrPipe } from './currency-inr.pipe';

describe('IstDatePipe', () => {
  const pipe = new IstDatePipe();

  it('returns empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('formats a datetime without shifting the clock', () => {
    // 14:30 in the API string must still read 02:30 PM, not be offset
    const out = pipe.transform('2026-06-19T14:30:00', 'datetime');
    expect(out).toContain('2026');
    expect(out).toMatch(/2:30/);
  });

  it('date mode omits time', () => {
    const out = pipe.transform('2026-06-19T14:30:00', 'date');
    expect(out).not.toMatch(/2:30/);
  });
});

describe('CurrencyInrPipe', () => {
  const pipe = new CurrencyInrPipe();

  it('formats with rupee symbol and two decimals', () => {
    expect(pipe.transform(1200)).toBe('₹1,200.00');
  });

  it('returns ₹0.00 for null', () => {
    expect(pipe.transform(null)).toBe('₹0.00');
  });
});

import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'istDate', standalone: true })
export class IstDatePipe implements PipeTransform {
  transform(value: string | Date | null, mode: 'date' | 'datetime' = 'datetime'): string {
    if (!value) return '';
    // The backend already emits IST wall-clock times. Parse the components
    // directly so we format exactly what was sent, with no timezone shift.
    const iso = typeof value === 'string' ? value : value.toISOString();
    const m = iso.match(/(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/);
    if (!m) return iso;
    const [, y, mo, d, h, min] = m;
    const date = new Date(+y, +mo - 1, +d, +h, +min);
    const dateStr = date.toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' });
    if (mode === 'date') return dateStr;
    const timeStr = date.toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit', hour12: true });
    return `${dateStr}, ${timeStr}`;
  }
}

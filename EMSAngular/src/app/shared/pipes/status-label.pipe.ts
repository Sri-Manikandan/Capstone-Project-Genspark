import { Pipe, PipeTransform } from '@angular/core';

/**
 * Renders a PascalCase status constant as prose: "PendingApproval" → "Pending approval".
 * Statuses are stored as plain strings, so the raw value otherwise leaks into the UI.
 */
@Pipe({ name: 'statusLabel', standalone: true })
export class StatusLabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    const words = value.replace(/([a-z])([A-Z])/g, '$1 $2').split(' ');
    return words
      .map((w, i) => (i === 0 ? w : w.toLowerCase()))
      .join(' ');
  }
}

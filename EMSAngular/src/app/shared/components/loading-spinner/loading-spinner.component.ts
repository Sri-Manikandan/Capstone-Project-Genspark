import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ems-loading-spinner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex justify-center py-10">
      <div class="h-8 w-8 animate-spin rounded-full border-4 border-gray-200 border-t-indigo-600"></div>
    </div>
  `,
})
export class LoadingSpinnerComponent {}

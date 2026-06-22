import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ems-loading-spinner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex justify-center py-12">
      <div class="h-8 w-8 animate-spin rounded-full border-[3px] border-line border-t-plum"></div>
    </div>
  `,
})
export class LoadingSpinnerComponent {}

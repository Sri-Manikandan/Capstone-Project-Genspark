import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ems-loading-spinner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './loading-spinner.component.html',
})
export class LoadingSpinnerComponent {}

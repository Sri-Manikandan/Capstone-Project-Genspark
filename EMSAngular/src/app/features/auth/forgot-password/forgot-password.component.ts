import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Reset password</h1>
      <ems-alert [type]="resetToken() ? 'success' : 'error'" [message]="message()" (dismissed)="message.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="email" type="email" placeholder="Email" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <button type="submit" class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Send reset token</button>
      </form>
      <a *ngIf="resetToken()" [routerLink]="['/auth/reset-password']" [queryParams]="{ token: resetToken() }"
         class="mt-3 block text-sm text-indigo-600 hover:underline">Continue to reset &rarr;</a>
    </div>
  `,
})
export class ForgotPasswordComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);

  protected message = signal('');
  protected resetToken = signal('');
  protected form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.auth.forgotPassword(this.form.getRawValue()).subscribe({
      next: res => { this.message.set(res.message); this.resetToken.set(res.resetToken); },
      error: (msg: string) => this.message.set(msg),
    });
  }
}

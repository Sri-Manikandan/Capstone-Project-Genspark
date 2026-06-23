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
    <div class="mx-auto max-w-sm">
      <div class="card p-7">
        <p class="eyebrow text-plum">Lost your way in?</p>
        <h1 class="mt-2 font-display text-2xl font-semibold text-ink">Reset password</h1>
        <p class="mt-1 mb-5 text-sm text-muted">Enter your email and we'll send a reset token.</p>
        <ems-alert [type]="resetToken() ? 'success' : 'error'" [message]="message()" (dismissed)="message.set('')" />
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <label class="block space-y-1">
            <span class="field-label">Email</span>
            <input formControlName="email" type="email" placeholder="you@example.com" class="field" />
          </label>
          <button type="submit" class="btn-primary w-full">Send reset token</button>
        </form>
        <a *ngIf="resetToken()" [routerLink]="['/auth/reset-password']" [queryParams]="{ token: resetToken() }"
           class="mt-4 block text-sm link-action">Continue to reset &rarr;</a>
      </div>
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

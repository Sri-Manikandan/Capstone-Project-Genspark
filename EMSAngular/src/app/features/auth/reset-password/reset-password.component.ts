import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm">
      <div class="card p-7">
        <p class="eyebrow text-plum">Almost in</p>
        <h1 class="mt-2 font-display text-2xl font-semibold text-ink">Set new password</h1>
        <p class="mt-1 mb-5 text-sm text-muted">Paste your reset token and choose a new password.</p>
        <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <label class="block space-y-1">
            <span class="field-label">Reset token</span>
            <input formControlName="token" placeholder="Reset token" class="field" />
          </label>
          <label class="block space-y-1">
            <span class="field-label">New password</span>
            <input formControlName="newPassword" type="password" placeholder="New password (min 8)" class="field" />
          </label>
          <button type="submit" class="btn-primary w-full">Reset password</button>
        </form>
      </div>
    </div>
  `,
})
export class ResetPasswordComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected error = signal('');
  protected form = this.fb.nonNullable.group({
    token: [this.route.snapshot.queryParamMap.get('token') ?? '', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.auth.resetPassword(this.form.getRawValue()).subscribe({
      next: () => this.router.navigateByUrl('/auth/login'),
      error: (msg: string) => this.error.set(msg),
    });
  }
}

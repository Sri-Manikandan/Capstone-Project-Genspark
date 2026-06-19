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
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Set new password</h1>
      <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="token" placeholder="Reset token" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="newPassword" type="password" placeholder="New password (min 8)" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <button type="submit" class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Reset password</button>
      </form>
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

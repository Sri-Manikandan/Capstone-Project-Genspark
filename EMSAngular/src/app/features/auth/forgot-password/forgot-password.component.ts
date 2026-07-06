import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';

@Component({
  selector: 'ems-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forgot-password.component.html',
})
export class ForgotPasswordComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);

  protected message = signal('');
  protected messageType = signal<'success' | 'error' | 'info'>('info');
  protected resetToken = signal('');
  protected form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.auth.forgotPassword(this.form.getRawValue()).subscribe({
      next: res => {
        this.message.set(res.message);
        this.resetToken.set(res.resetToken);
        // A returned token is an actionable success; an empty token is the privacy-preserving
        // "if that email exists…" acknowledgement — informational, not an error.
        this.messageType.set(res.resetToken ? 'success' : 'info');
      },
      error: (msg: string) => { this.message.set(msg); this.messageType.set('error'); },
    });
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm">
      <div class="card p-7">
        <p class="eyebrow text-plum">Box office</p>
        <h1 class="mt-2 font-display text-2xl font-semibold text-ink">Welcome back</h1>
        <p class="mt-1 mb-5 text-sm text-muted">Sign in to see your tickets and bookings.</p>
        <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <label class="block space-y-1">
            <span class="field-label">Email</span>
            <input formControlName="email" type="email" placeholder="you@example.com" class="field" />
          </label>
          <label class="block space-y-1">
            <span class="field-label">Password</span>
            <input formControlName="password" type="password" placeholder="Password" class="field" />
          </label>
          <button type="submit" [disabled]="submitting()" class="btn-primary w-full">
            {{ submitting() ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>
        <div class="mt-4 flex justify-between text-sm">
          <a routerLink="/auth/register" class="link-action">Create account</a>
          <a routerLink="/auth/forgot-password" class="link-action">Forgot password?</a>
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  protected submitting = signal(false);
  error = signal('');
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.error.set('');
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/events';
        this.router.navigateByUrl(returnUrl);
      },
      error: (msg: string) => { this.error.set(msg); this.submitting.set(false); },
    });
  }
}

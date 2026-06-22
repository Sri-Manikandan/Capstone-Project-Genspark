import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-sm">
      <div class="card p-7">
        <p class="eyebrow text-plum">Join the queue</p>
        <h1 class="mt-2 font-display text-2xl font-semibold text-ink">Create your account</h1>
        <p class="mt-1 mb-5 text-sm text-muted">Book tickets and keep them all in one place.</p>
        <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-3">
          <input formControlName="name" placeholder="Full name" class="field" />
          <input formControlName="email" type="email" placeholder="Email" class="field" />
          <input formControlName="phone" placeholder="Phone" class="field" />
          <input formControlName="password" type="password" placeholder="Password (min 8)" class="field" />
          <button type="submit" [disabled]="submitting()" class="btn-primary w-full">
            {{ submitting() ? 'Creating…' : 'Create account' }}
          </button>
        </form>
        <a routerLink="/auth/login" class="mt-4 block text-sm link-action">Already have an account?</a>
      </div>
    </div>
  `,
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  protected submitting = signal(false);
  protected error = signal('');
  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.pattern(/^\+?[0-9]{7,15}$/)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.error.set('');
    this.auth.register({ ...this.form.getRawValue(), role: 'User' }).subscribe({
      next: () => this.router.navigateByUrl('/events'),
      error: (msg: string) => { this.error.set(msg); this.submitting.set(false); },
    });
  }
}

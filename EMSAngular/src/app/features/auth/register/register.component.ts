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
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Create account</h1>
      <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="name" placeholder="Full name" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="email" type="email" placeholder="Email" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="phone" placeholder="Phone" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <input formControlName="password" type="password" placeholder="Password (min 8)" class="w-full rounded-lg border border-gray-300 px-3 py-2" />
        <button type="submit" [disabled]="submitting()"
                class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">Register</button>
      </form>
      <a routerLink="/auth/login" class="mt-3 block text-sm text-indigo-600 hover:underline">Already have an account?</a>
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

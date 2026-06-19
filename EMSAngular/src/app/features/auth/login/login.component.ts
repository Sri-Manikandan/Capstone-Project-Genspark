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
    <div class="mx-auto max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <h1 class="mb-4 text-xl font-semibold text-gray-900">Sign in</h1>
      <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4">
        <input formControlName="email" type="email" placeholder="Email"
               class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:ring-2 focus:ring-indigo-500" />
        <input formControlName="password" type="password" placeholder="Password"
               class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:ring-2 focus:ring-indigo-500" />
        <button type="submit" [disabled]="submitting()"
                class="w-full rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50">Sign in</button>
      </form>
      <div class="mt-3 flex justify-between text-sm">
        <a routerLink="/auth/register" class="text-indigo-600 hover:underline">Create account</a>
        <a routerLink="/auth/forgot-password" class="text-indigo-600 hover:underline">Forgot password?</a>
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

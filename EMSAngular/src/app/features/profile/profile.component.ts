import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { UserService } from '../../core/services/user.service';
import { AuthService } from '../../core/services/auth.service';
import { User } from '../../core/models/user.model';
import { OrganizerRequestDto } from '../../core/models/admin.model';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../shared/components/alert/alert.component';
import { FieldErrorComponent } from '../../shared/components/field-error/field-error.component';
import { IstDatePipe } from '../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-profile',
  standalone: true,
  imports: [CommonModule, RouterLink, ReactiveFormsModule, LoadingSpinnerComponent, AlertComponent, FieldErrorComponent, IstDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit {
  private userService = inject(UserService);
  private auth = inject(AuthService);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  protected me = signal<User | null>(null);
  protected request = signal<OrganizerRequestDto | null>(null);
  protected loading = signal(false);
  protected submitting = signal(false);
  protected changingPassword = signal(false);
  protected savingProfile = signal(false);
  protected changingEmail = signal(false);
  protected closing = signal(false);

  // Page-level load failure; each form owns its own feedback below.
  protected loadError = signal('');
  protected requestError = signal('');
  protected requestSuccess = signal('');
  protected passwordError = signal('');
  protected passwordSuccess = signal('');
  protected profileError = signal('');
  protected profileSuccess = signal('');
  protected emailError = signal('');
  protected emailSuccess = signal('');
  protected closeError = signal('');

  protected canRequest = computed(() => this.me()?.role === 'User');

  protected form = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.minLength(10)]],
  });

  protected passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8), passwordComplexity]],
    confirmPassword: ['', Validators.required],
  }, { validators: passwordsMatch });

  protected profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.pattern(/^\+?[0-9]{7,15}$/)]],
  });

  protected emailForm = this.fb.nonNullable.group({
    newEmail: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected closeForm = this.fb.nonNullable.group({
    password: ['', Validators.required],
  });

  ngOnInit(): void {
    this.loading.set(true);
    this.userService.getMe().subscribe({
      next: u => {
        this.me.set(u);
        this.profileForm.patchValue({ name: u.name, phone: u.phone });
        this.loading.set(false);
        if (u.role === 'User') this.loadRequest();
      },
      error: (msg: string) => { this.loadError.set(msg); this.loading.set(false); },
    });
  }

  protected submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.requestError.set('');
    this.requestSuccess.set('');
    this.submitting.set(true);
    this.userService.requestOrganizer(this.form.getRawValue().reason).subscribe({
      next: r => {
        this.request.set(r);
        this.requestSuccess.set('Request submitted. An admin will review it soon.');
        this.form.reset();
        this.submitting.set(false);
      },
      error: (msg: string) => { this.requestError.set(msg); this.submitting.set(false); },
    });
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid) { this.passwordForm.markAllAsTouched(); return; }
    this.passwordError.set('');
    this.passwordSuccess.set('');
    this.changingPassword.set(true);
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.userService.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.passwordSuccess.set('Password updated.');
        this.passwordForm.reset();
        this.changingPassword.set(false);
      },
      error: (msg: string) => { this.passwordError.set(msg); this.changingPassword.set(false); },
    });
  }

  protected saveProfile(): void {
    if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
    this.profileError.set('');
    this.profileSuccess.set('');
    this.savingProfile.set(true);
    this.userService.updateMe(this.profileForm.getRawValue()).subscribe({
      next: u => {
        this.me.set(u);
        this.auth.setCurrentUser(u);
        this.profileSuccess.set('Profile updated.');
        this.savingProfile.set(false);
      },
      error: (msg: string) => { this.profileError.set(msg); this.savingProfile.set(false); },
    });
  }

  protected changeEmail(): void {
    if (this.emailForm.invalid) { this.emailForm.markAllAsTouched(); return; }
    this.emailError.set('');
    this.emailSuccess.set('');
    this.changingEmail.set(true);
    this.userService.changeEmail(this.emailForm.getRawValue()).subscribe({
      next: u => {
        this.me.set(u);
        this.auth.setCurrentUser(u);
        this.emailSuccess.set('Email updated.');
        this.emailForm.reset();
        this.changingEmail.set(false);
      },
      error: (msg: string) => { this.emailError.set(msg); this.changingEmail.set(false); },
    });
  }

  protected closeAccount(): void {
    if (this.closeForm.invalid) { this.closeForm.markAllAsTouched(); return; }
    if (!confirm('Permanently close your account? This cannot be undone.')) return;
    this.closeError.set('');
    this.closing.set(true);
    this.userService.deleteMe(this.closeForm.getRawValue()).subscribe({
      next: () => {
        this.auth.logout();
        this.router.navigate(['/events']);
      },
      error: (msg: string) => { this.closeError.set(msg); this.closing.set(false); },
    });
  }

  // The service maps a 404 (never requested) to null; only real failures reach `loadError`.
  private loadRequest(): void {
    this.userService.getOrganizerRequest().subscribe({
      next: r => this.request.set(r),
      error: (msg: string) => { this.request.set(null); this.loadError.set(msg); },
    });
  }
}

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const next = group.get('newPassword')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return next && confirm && next !== confirm ? { mismatch: true } : null;
}

// Mirrors the backend InputValidator: an upper, a lower, a digit, and a special character.
function passwordComplexity(control: AbstractControl): ValidationErrors | null {
  const value: string = control.value ?? '';
  if (!value) return null;
  const hasComplexity = /[A-Z]/.test(value) && /[a-z]/.test(value) && /[0-9]/.test(value) && /[^a-zA-Z0-9]/.test(value);
  return hasComplexity ? null : { complexity: true };
}

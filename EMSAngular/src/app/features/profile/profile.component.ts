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
import { IstDatePipe } from '../../shared/pipes/ist-date.pipe';

@Component({
  selector: 'ems-profile',
  standalone: true,
  imports: [CommonModule, RouterLink, ReactiveFormsModule, LoadingSpinnerComponent, AlertComponent, IstDatePipe],
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
  protected error = signal('');
  protected success = signal('');

  protected canRequest = computed(() => this.me()?.role === 'User');

  protected form = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.minLength(10)]],
  });

  protected passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  }, { validators: passwordsMatch });

  protected profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    phone: ['', Validators.required],
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
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }

  protected submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);
    this.userService.requestOrganizer(this.form.getRawValue().reason).subscribe({
      next: r => {
        this.request.set(r);
        this.success.set('Request submitted. An admin will review it soon.');
        this.form.reset();
        this.submitting.set(false);
      },
      error: (msg: string) => { this.error.set(msg); this.submitting.set(false); },
    });
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid) { this.passwordForm.markAllAsTouched(); return; }
    this.changingPassword.set(true);
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.userService.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.success.set('Password updated.');
        this.passwordForm.reset();
        this.changingPassword.set(false);
      },
      error: (msg: string) => { this.error.set(msg); this.changingPassword.set(false); },
    });
  }

  protected saveProfile(): void {
    if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
    this.savingProfile.set(true);
    this.userService.updateMe(this.profileForm.getRawValue()).subscribe({
      next: u => {
        this.me.set(u);
        this.auth.setCurrentUser(u);
        this.success.set('Profile updated.');
        this.savingProfile.set(false);
      },
      error: (msg: string) => { this.error.set(msg); this.savingProfile.set(false); },
    });
  }

  protected changeEmail(): void {
    if (this.emailForm.invalid) { this.emailForm.markAllAsTouched(); return; }
    this.changingEmail.set(true);
    this.userService.changeEmail(this.emailForm.getRawValue()).subscribe({
      next: u => {
        this.me.set(u);
        this.auth.setCurrentUser(u);
        this.success.set('Email updated.');
        this.emailForm.reset();
        this.changingEmail.set(false);
      },
      error: (msg: string) => { this.error.set(msg); this.changingEmail.set(false); },
    });
  }

  protected closeAccount(): void {
    if (this.closeForm.invalid) { this.closeForm.markAllAsTouched(); return; }
    if (!confirm('Permanently close your account? This cannot be undone.')) return;
    this.closing.set(true);
    this.userService.deleteMe(this.closeForm.getRawValue()).subscribe({
      next: () => {
        this.auth.logout();
        this.router.navigate(['/events']);
      },
      error: (msg: string) => { this.error.set(msg); this.closing.set(false); },
    });
  }

  // A 404 here simply means the user has never requested — treat as no request.
  private loadRequest(): void {
    this.userService.getOrganizerRequest().subscribe({
      next: r => this.request.set(r),
      error: () => this.request.set(null),
    });
  }
}

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const next = group.get('newPassword')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return next && confirm && next !== confirm ? { mismatch: true } : null;
}

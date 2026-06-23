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
  template: `
    <p class="eyebrow text-plum">Your account</p>
    <h1 class="page-title mt-2 mb-7">Profile</h1>

    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />
    <ems-alert type="success" [message]="success()" (dismissed)="success.set('')" />
    <ems-loading-spinner *ngIf="loading()" />

    <div *ngIf="me() as u" class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <!-- ── Account details ─────────────────────────────────────── -->
      <section class="card p-6">
        <h2 class="eyebrow mb-4">Details</h2>
        <div class="flex items-center gap-4">
          <span class="grid h-14 w-14 place-items-center rounded-full bg-plum font-display text-xl font-semibold text-white">
            {{ u.name.charAt(0).toUpperCase() }}
          </span>
          <div>
            <p class="font-display text-xl font-semibold text-ink">{{ u.name }}</p>
            <span class="badge bg-plum-tint text-plum-dark">{{ u.role }}</span>
          </div>
        </div>
        <dl class="mt-6 space-y-3 text-sm">
          <div class="flex justify-between gap-4 border-t border-line pt-3">
            <dt class="text-muted">Email</dt><dd class="text-ink-soft">{{ u.email }}</dd>
          </div>
          <div class="flex justify-between gap-4 border-t border-line pt-3">
            <dt class="text-muted">Phone</dt><dd class="text-ink-soft">{{ u.phone }}</dd>
          </div>
          <div class="flex justify-between gap-4 border-t border-line pt-3">
            <dt class="text-muted">Member since</dt><dd class="font-mono text-ink-soft">{{ u.createdAt | istDate: 'date' }}</dd>
          </div>
        </dl>
      </section>

      <!-- ── Organizer access ────────────────────────────────────── -->
      <section class="card p-6">
        <h2 class="eyebrow mb-4">Organizer access</h2>

        <!-- Already an organizer / admin -->
        <ng-container *ngIf="!canRequest(); else userSection">
          <p class="text-sm text-ink-soft">You can create and manage events.</p>
          <a routerLink="/organizer/events" class="btn-primary mt-5">Go to My Events</a>
        </ng-container>

        <ng-template #userSection>
          <!-- Existing request -->
          <ng-container *ngIf="request() as r; else requestForm">
            <span class="badge"
                  [class.bg-gold-tint]="r.status === 'Pending'" [class.text-gold]="r.status === 'Pending'"
                  [class.bg-teal-tint]="r.status === 'Approved'" [class.text-teal-dark]="r.status === 'Approved'"
                  [class.bg-rose-tint]="r.status === 'Rejected'" [class.text-rose-dark]="r.status === 'Rejected'">
              {{ r.status }}
            </span>
            <p class="mt-3 text-sm text-ink-soft" [ngSwitch]="r.status">
              <span *ngSwitchCase="'Pending'">Your request is in review. We'll update your role once an admin approves it.</span>
              <span *ngSwitchCase="'Rejected'">Your previous request wasn't approved. You're welcome to submit a new one below.</span>
              <span *ngSwitchDefault>Your request has been approved — sign out and back in to refresh your access.</span>
            </p>
            <p class="mt-1 font-mono text-xs text-muted">Requested {{ r.requestedAt | istDate }}</p>
            <p *ngIf="r.reason" class="mt-3 rounded-xl bg-paper p-3 text-sm text-ink-soft">"{{ r.reason }}"</p>

            <form *ngIf="r.status === 'Rejected'" [formGroup]="form" (ngSubmit)="submit()" class="mt-5 space-y-3">
              <label class="space-y-1 block">
                <span class="field-label">Why do you want to organize events?</span>
                <textarea formControlName="reason" rows="3" class="field" placeholder="Tell the team about the events you'd like to run."></textarea>
              </label>
              <button type="submit" [disabled]="submitting()" class="btn-primary">Submit new request</button>
            </form>
          </ng-container>

          <!-- No request yet -->
          <ng-template #requestForm>
            <p class="text-sm text-ink-soft">Want to host your own events? Request organizer access and an admin will review it.</p>
            <form [formGroup]="form" (ngSubmit)="submit()" class="mt-5 space-y-3">
              <label class="space-y-1 block">
                <span class="field-label">Why do you want to organize events?</span>
                <textarea formControlName="reason" rows="3" class="field" placeholder="Tell the team about the events you'd like to run."></textarea>
              </label>
              <button type="submit" [disabled]="submitting()" class="btn-primary">Become an organizer</button>
            </form>
          </ng-template>
        </ng-template>
      </section>

      <!-- ── Security ────────────────────────────────────────────── -->
      <section class="card p-6 lg:col-span-2">
        <h2 class="eyebrow mb-4">Security</h2>
        <form [formGroup]="passwordForm" (ngSubmit)="changePassword()" class="grid max-w-xl grid-cols-1 gap-4">
          <label class="space-y-1 block">
            <span class="field-label">Current password</span>
            <input type="password" formControlName="currentPassword" autocomplete="current-password" class="field" />
          </label>
          <label class="space-y-1 block">
            <span class="field-label">New password</span>
            <input type="password" formControlName="newPassword" autocomplete="new-password" class="field" />
            <span *ngIf="passwordForm.controls.newPassword.touched && passwordForm.controls.newPassword.errors?.['minlength']"
                  class="text-xs text-rose">Use at least 8 characters.</span>
          </label>
          <label class="space-y-1 block">
            <span class="field-label">Confirm new password</span>
            <input type="password" formControlName="confirmPassword" autocomplete="new-password" class="field" />
            <span *ngIf="passwordForm.controls.confirmPassword.touched && passwordForm.errors?.['mismatch']"
                  class="text-xs text-rose">Passwords don't match.</span>
          </label>
          <div>
            <button type="submit" [disabled]="changingPassword()" class="btn-primary">Update password</button>
          </div>
        </form>
      </section>

      <!-- ── Edit profile ────────────────────────────────────────── -->
      <section class="card p-6">
        <h2 class="eyebrow mb-4">Edit profile</h2>
        <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="space-y-4">
          <label class="space-y-1 block">
            <span class="field-label">Name</span>
            <input formControlName="name" autocomplete="name" class="field" />
          </label>
          <label class="space-y-1 block">
            <span class="field-label">Phone</span>
            <input formControlName="phone" autocomplete="tel" class="field" />
          </label>
          <button type="submit" [disabled]="savingProfile()" class="btn-primary">Save changes</button>
        </form>
      </section>

      <!-- ── Change email ────────────────────────────────────────── -->
      <section class="card p-6">
        <h2 class="eyebrow mb-4">Change email</h2>
        <form [formGroup]="emailForm" (ngSubmit)="changeEmail()" class="space-y-4">
          <label class="space-y-1 block">
            <span class="field-label">New email</span>
            <input type="email" formControlName="newEmail" autocomplete="email" class="field" />
          </label>
          <label class="space-y-1 block">
            <span class="field-label">Current password</span>
            <input type="password" formControlName="password" autocomplete="current-password" class="field" />
          </label>
          <button type="submit" [disabled]="changingEmail()" class="btn-primary">Update email</button>
        </form>
      </section>

      <!-- ── Danger zone ─────────────────────────────────────────── -->
      <section class="card border-rose/40 p-6 lg:col-span-2">
        <h2 class="eyebrow mb-1 text-rose">Danger zone</h2>
        <p class="mb-4 text-sm text-ink-soft">Closing your account is permanent. Enter your password to confirm.</p>
        <form [formGroup]="closeForm" (ngSubmit)="closeAccount()" class="flex max-w-xl flex-col gap-4 sm:flex-row sm:items-end">
          <label class="block flex-1 space-y-1">
            <span class="field-label">Password</span>
            <input type="password" formControlName="password" autocomplete="current-password" class="field" />
          </label>
          <button type="submit" [disabled]="closing()" class="btn-danger">Close account</button>
        </form>
      </section>
    </div>
  `,
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

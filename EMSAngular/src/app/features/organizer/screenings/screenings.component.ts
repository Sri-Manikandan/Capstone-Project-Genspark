import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ScreeningService } from '../../../core/services/screening.service';
import { ScreeningDto } from '../../../core/models/screening.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { FieldErrorComponent } from '../../../shared/components/field-error/field-error.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { OrganizerEventNavComponent } from '../event-nav/organizer-event-nav.component';

@Component({
  selector: 'ems-screenings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertComponent, FieldErrorComponent, LoadingSpinnerComponent, IstDatePipe, OrganizerEventNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './screenings.component.html',
})
export class ScreeningsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private service = inject(ScreeningService);

  protected screenings = signal<ScreeningDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected editingId = signal<number | null>(null);
  protected eventId = Number(this.route.snapshot.paramMap.get('id'));

  protected form = this.fb.nonNullable.group({
    screen: ['', [Validators.required, Validators.minLength(1)]],
    startTime: ['', Validators.required],
    endTime: ['', Validators.required],
  });

  ngOnInit(): void { this.load(); }

  protected save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const editingId = this.editingId();
    const done = () => { this.cancelEdit(); this.load(); };

    if (editingId !== null) {
      this.service.update(editingId, value).subscribe({ next: done, error: (m: string) => this.error.set(m) });
    } else {
      this.service.create({ eventId: this.eventId, ...value }).subscribe({ next: done, error: (m: string) => this.error.set(m) });
    }
  }

  protected edit(s: ScreeningDto): void {
    this.editingId.set(s.id);
    this.form.setValue({
      screen: s.screen,
      startTime: s.startTime.slice(0, 16),
      endTime: s.endTime.slice(0, 16),
    });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ screen: '', startTime: '', endTime: '' });
  }

  protected remove(id: number): void {
    this.service.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.service.getByEvent(this.eventId).subscribe({
      next: s => { this.screenings.set(s); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}

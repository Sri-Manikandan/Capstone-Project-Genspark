import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { TicketTypeDto } from '../../../core/models/ticket-type.model';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { CurrencyInrPipe } from '../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-ticket-types',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, AlertComponent, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="eyebrow text-plum">Organizer</p>
    <h1 class="page-title mt-2 mb-6">Ticket types</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <ul class="mb-8 space-y-2">
      <li *ngFor="let t of ticketTypes()" class="flex items-center justify-between gap-4 rounded-xl border border-line bg-surface p-4">
        <span class="text-sm text-ink"><span class="font-display text-base font-semibold">{{ t.name }}</span>
          <span class="font-mono text-xs text-muted"> · {{ t.seatType }} · {{ t.price | inr }} · {{ t.availableQuantity }}/{{ t.totalQuantity }} left</span></span>
        <button (click)="remove(t.id)" class="link-danger">Delete</button>
      </li>
      <li *ngIf="ticketTypes().length === 0" class="card px-6 py-12 text-center text-muted">No ticket types yet.</li>
    </ul>

    <form [formGroup]="form" (ngSubmit)="add()" class="card grid max-w-xl grid-cols-1 gap-3 p-6 sm:grid-cols-2">
      <input formControlName="name" placeholder="Name" class="field" />
      <input formControlName="seatType" placeholder="Seat type" class="field" />
      <input formControlName="price" type="number" placeholder="Price" class="field" />
      <input formControlName="totalQuantity" type="number" placeholder="Quantity" class="field" />
      <label class="space-y-1"><span class="field-label">Sale start</span><input formControlName="saleStart" type="datetime-local" class="field" /></label>
      <label class="space-y-1"><span class="field-label">Sale end</span><input formControlName="saleEnd" type="datetime-local" class="field" /></label>
      <button type="submit" class="btn-primary sm:col-span-2">Add ticket type</button>
    </form>
  `,
})
export class TicketTypesComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private service = inject(TicketTypeService);

  protected ticketTypes = signal<TicketTypeDto[]>([]);
  protected error = signal('');
  private eventId = Number(this.route.snapshot.paramMap.get('id'));

  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    seatType: ['', [Validators.required, Validators.minLength(2)]],
    price: [0, [Validators.min(0)]],
    totalQuantity: [1, [Validators.min(1)]],
    saleStart: ['', Validators.required],
    saleEnd: ['', Validators.required],
  });

  ngOnInit(): void { this.load(); }

  protected add(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.service.create({ eventId: this.eventId, ...this.form.getRawValue() }).subscribe({
      next: () => { this.form.reset(); this.load(); },
      error: (m: string) => this.error.set(m),
    });
  }

  protected remove(id: number): void {
    this.service.delete(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.service.getByEvent(this.eventId).subscribe({
      next: t => this.ticketTypes.set(t), error: (m: string) => this.error.set(m),
    });
  }
}

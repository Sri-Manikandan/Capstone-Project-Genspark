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
    <h1 class="mb-4 text-2xl font-semibold text-gray-900">Ticket Types</h1>
    <ems-alert type="error" [message]="error()" (dismissed)="error.set('')" />

    <ul class="mb-6 space-y-2">
      <li *ngFor="let t of ticketTypes()" class="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-3">
        <span>{{ t.name }} ({{ t.seatType }}) · {{ t.price | inr }} · {{ t.availableQuantity }}/{{ t.totalQuantity }}</span>
        <button (click)="remove(t.id)" class="text-red-600 hover:underline">Delete</button>
      </li>
      <li *ngIf="ticketTypes().length === 0" class="text-gray-500">No ticket types yet.</li>
    </ul>

    <form [formGroup]="form" (ngSubmit)="add()" class="grid max-w-xl grid-cols-2 gap-3">
      <input formControlName="name" placeholder="Name" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="seatType" placeholder="Seat type" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="price" type="number" placeholder="Price" class="rounded-lg border border-gray-300 px-3 py-2" />
      <input formControlName="totalQuantity" type="number" placeholder="Quantity" class="rounded-lg border border-gray-300 px-3 py-2" />
      <label class="text-sm text-gray-600">Sale start<input formControlName="saleStart" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" /></label>
      <label class="text-sm text-gray-600">Sale end<input formControlName="saleEnd" type="datetime-local" class="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2" /></label>
      <button type="submit" class="col-span-2 rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">Add ticket type</button>
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

import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TicketTypeDto } from '../../../../core/models/ticket-type.model';
import { CurrencyInrPipe } from '../../../../shared/pipes/currency-inr.pipe';

@Component({
  selector: 'ems-ticket-picker',
  standalone: true,
  imports: [CommonModule, CurrencyInrPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-picker.component.html',
})
export class TicketPickerComponent {
  @Input({ required: true }) ticketTypes: TicketTypeDto[] = [];
  @Input() maxQuantity = 10;
  @Output() confirmed = new EventEmitter<{ ticketType: TicketTypeDto; quantity: number }>();
  @Output() closed = new EventEmitter<void>();

  protected selectedId = signal<number | null>(null);
  protected quantity = signal(1);

  protected select(t: TicketTypeDto): void {
    this.selectedId.set(t.id);
  }

  protected changeQuantity(delta: number): void {
    this.quantity.update(q => Math.min(this.maxQuantity, Math.max(1, q + delta)));
  }

  protected confirm(): void {
    const ticketType = this.ticketTypes.find(t => t.id === this.selectedId());
    if (!ticketType) return;
    this.confirmed.emit({ ticketType, quantity: this.quantity() });
  }
}

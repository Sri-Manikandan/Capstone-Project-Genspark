import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { EventDto } from '../../../core/models/event.model';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';
import { ModalComponent } from '../../../shared/components/modal/modal.component';
import { IstDatePipe } from '../../../shared/pipes/ist-date.pipe';
import { StatusLabelPipe } from '../../../shared/pipes/status-label.pipe';

@Component({
  selector: 'ems-organizer-event-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PaginationComponent, LoadingSpinnerComponent, AlertComponent, ModalComponent, IstDatePipe, StatusLabelPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './organizer-event-list.component.html',
})
export class OrganizerEventListComponent implements OnInit {
  private eventService = inject(EventService);

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected page = signal(1);
  protected totalPages = signal(1);
  // Id of the event awaiting cancel confirmation; null when the dialog is closed.
  protected cancelTargetId = signal<number | null>(null);

  ngOnInit(): void { this.load(); }
  protected goToPage(p: number): void { this.page.set(p); this.load(); }

  protected submitEvent(id: number): void {
    this.eventService.submit(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  protected requestCancel(id: number): void {
    this.cancelTargetId.set(id);
  }

  protected dismissCancel(): void {
    this.cancelTargetId.set(null);
  }

  protected confirmCancel(): void {
    const id = this.cancelTargetId();
    if (id === null) return;
    this.cancelTargetId.set(null);
    this.eventService.cancel(id).subscribe({ next: () => this.load(), error: (m: string) => this.error.set(m) });
  }

  private load(): void {
    this.loading.set(true);
    this.eventService.getMyEvents(this.page(), 10).subscribe({
      next: res => { this.events.set(res.items); this.totalPages.set(res.totalPages); this.loading.set(false); },
      error: (m: string) => { this.error.set(m); this.loading.set(false); },
    });
  }
}

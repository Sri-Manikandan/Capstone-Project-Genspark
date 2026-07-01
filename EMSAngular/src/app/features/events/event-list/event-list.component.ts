import { ChangeDetectionStrategy, Component, OnInit, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EventService } from '../../../core/services/event.service';
import { LocationService } from '../../../core/services/location.service';
import { EventDto, EventSearchRequest } from '../../../core/models/event.model';
import { EventFilterStore } from '../event-filter.store';
import { EventCardComponent } from '../../../shared/components/event-card/event-card.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AlertComponent } from '../../../shared/components/alert/alert.component';

@Component({
  selector: 'ems-event-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, EventCardComponent, PaginationComponent, LoadingSpinnerComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './event-list.component.html',
})
export class EventListComponent implements OnInit {
  private eventService = inject(EventService);
  private locationService = inject(LocationService);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);
  protected store = inject(EventFilterStore);

  // Remembers the user's explicit city choice ('' = "All cities") so we only
  // auto-detect location on their very first visit.
  private static readonly CITY_KEY = 'ems-city';

  protected events = signal<EventDto[]>([]);
  protected loading = signal(false);
  protected error = signal('');
  protected totalPages = signal(1);
  protected detectingLocation = signal(false);
  protected search = this.fb.nonNullable.control('');

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(query => this.store.patch({ query }));

    effect(() => {
      const req = this.store.request();
      this.load(req);
    });
  }

  ngOnInit(): void {
    const category = this.route.snapshot.queryParamMap.get('category');
    if (category) this.store.patch({ category });
    this.search.setValue(this.store.filters().query, { emitEvent: false });
    this.store.loadCategories();
    this.initCity();
  }

  // Load the city list for the switcher, then apply a remembered choice or
  // auto-detect the user's city on their first visit.
  private initCity(): void {
    this.store.loadCities().subscribe(cities => {
      const remembered = localStorage.getItem(EventListComponent.CITY_KEY);
      if (remembered !== null) {
        if (remembered && cities.includes(remembered)) this.store.patch({ city: remembered });
        return;
      }
      if (this.store.filters().city) return;

      this.detectingLocation.set(true);
      this.locationService.detectCity(cities).then(city => {
        this.detectingLocation.set(false);
        if (city) {
          this.store.patch({ city });
          localStorage.setItem(EventListComponent.CITY_KEY, city);
        }
      });
    });
  }

  protected setCity(city: string): void {
    this.store.patch({ city });
    localStorage.setItem(EventListComponent.CITY_KEY, city);
  }

  protected setCategory(category: string): void { this.store.patch({ category }); }
  protected setSort(value: string): void {
    const [sortBy, sortOrder] = value.split(':') as ['startTime' | 'title' | 'createdAt', 'asc' | 'desc'];
    this.store.patch({ sortBy, sortOrder });
  }
  protected setStartFrom(startFrom: string): void { this.store.patch({ startFrom }); }
  protected setStartTo(startTo: string): void { this.store.patch({ startTo }); }
  protected goToPage(p: number): void { this.store.setPage(p); }

  protected clearFilters(): void {
    this.store.reset();
    this.search.setValue('', { emitEvent: false });
  }

  private load(req: EventSearchRequest): void {
    this.loading.set(true);
    this.error.set('');
    this.eventService.search(req).subscribe({
      next: res => {
        this.events.set(res.items);
        this.totalPages.set(res.totalPages);
        this.loading.set(false);
      },
      error: (msg: string) => { this.error.set(msg); this.loading.set(false); },
    });
  }
}

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { TicketTypesComponent } from './ticket-types.component';
import { TicketTypeService } from '../../../core/services/ticket-type.service';
import { ScreeningService } from '../../../core/services/screening.service';
import { EventService } from '../../../core/services/event.service';
import { SeatService } from '../../../core/services/seat.service';

describe('TicketTypesComponent', () => {
  let component: TicketTypesComponent;
  let fixture: ComponentFixture<TicketTypesComponent>;

  const screening = { id: 3, eventId: 9, screen: 'Screen 1', startTime: '2999-07-01T20:00:00', endTime: '2999-07-01T23:00:00', status: 'Scheduled' };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketTypesComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '3' } } } },
        { provide: TicketTypeService, useValue: { getByScreening: () => of([]), create: vi.fn().mockReturnValue(of({ id: 1 })), delete: () => of(void 0) } },
        { provide: ScreeningService, useValue: { getById: () => of(screening) } },
        { provide: EventService, useValue: { getById: () => of({ id: 9, venueId: 1 }) } },
        { provide: SeatService, useValue: { getByVenue: () => of([{ id: 1, venueId: 1, section: 'A', row: '1', seatNumber: 1, seatType: 'VIP' }]) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TicketTypesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('rejects a name longer than 100 characters', () => {
    component['form'].controls.name.setValue('x'.repeat(101));
    expect(component['form'].controls.name.hasError('maxlength')).toBe(true);
  });

  it('rejects a blank (whitespace-only) name', () => {
    component['form'].controls.name.setValue('   ');
    expect(component['form'].controls.name.hasError('notBlank')).toBe(true);
  });

  it('rejects a price above the 100000 cap', () => {
    component['form'].controls.price.setValue(100001);
    expect(component['form'].controls.price.hasError('max')).toBe(true);
  });

  it('requires the sale end to be after the sale start', () => {
    component['form'].controls.saleStart.setValue('2999-07-01T18:00');
    component['form'].controls.saleEnd.setValue('2999-07-01T17:00');
    expect(component['form'].controls.saleEnd.hasError('endBeforeStart')).toBe(true);
  });

  it('rejects a sale end after the screening starts', () => {
    // Screening starts 2999-07-01T20:00; a sale ending at 21:00 is too late.
    component['form'].controls.saleStart.setValue('2999-07-01T10:00');
    component['form'].controls.saleEnd.setValue('2999-07-01T21:00');
    expect(component['form'].controls.saleEnd.hasError('saleAfterScreening')).toBe(true);
  });

  it('accepts a sale window that closes before the screening starts', () => {
    component['form'].controls.saleStart.setValue('2999-07-01T10:00');
    component['form'].controls.saleEnd.setValue('2999-07-01T19:00');
    expect(component['form'].controls.saleEnd.hasError('saleAfterScreening')).toBe(false);
    expect(component['form'].controls.saleEnd.hasError('endBeforeStart')).toBe(false);
  });
});

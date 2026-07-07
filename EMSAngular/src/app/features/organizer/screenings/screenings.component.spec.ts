import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ScreeningsComponent } from './screenings.component';
import { ScreeningService } from '../../../core/services/screening.service';
import { EventService } from '../../../core/services/event.service';

describe('ScreeningsComponent', () => {
  let component: ScreeningsComponent;
  let fixture: ComponentFixture<ScreeningsComponent>;

  // Event window: 2999-07-01 18:00 → 2999-07-02 02:00.
  const event = { id: 7, venueId: 1, startTime: '2999-07-01T18:00:00', endTime: '2999-07-02T02:00:00' };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScreeningsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: ScreeningService, useValue: { getByEvent: () => of([]), create: vi.fn(), update: vi.fn(), delete: () => of(void 0) } },
        { provide: EventService, useValue: { getById: () => of(event) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ScreeningsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('rejects a screen label longer than 50 characters', () => {
    component['form'].controls.screen.setValue('x'.repeat(51));
    expect(component['form'].controls.screen.hasError('maxlength')).toBe(true);
  });

  it('rejects a blank (whitespace-only) screen label', () => {
    component['form'].controls.screen.setValue('   ');
    expect(component['form'].controls.screen.hasError('notBlank')).toBe(true);
  });

  it('rejects a start time in the past', () => {
    const past = new Date(Date.now() - 60 * 60_000).toISOString().slice(0, 16);
    component['form'].controls.startTime.setValue(past);
    expect(component['form'].controls.startTime.hasError('notFuture')).toBe(true);
  });

  it('requires the end time to be after the start time', () => {
    component['form'].controls.startTime.setValue('2999-07-01T21:00');
    component['form'].controls.endTime.setValue('2999-07-01T20:00');
    expect(component['form'].controls.endTime.hasError('endBeforeStart')).toBe(true);
  });

  it('rejects a start time before the event begins', () => {
    // Event begins 2999-07-01T18:00; 17:00 is outside the window.
    component['form'].controls.startTime.setValue('2999-07-01T17:00');
    expect(component['form'].controls.startTime.hasError('outsideEventWindow')).toBe(true);
  });

  it('rejects an end time after the event ends', () => {
    // Event ends 2999-07-02T02:00; 03:00 is outside the window.
    component['form'].controls.endTime.setValue('2999-07-02T03:00');
    expect(component['form'].controls.endTime.hasError('outsideEventWindow')).toBe(true);
  });

  it('accepts a screening that falls inside the event window', () => {
    component['form'].controls.startTime.setValue('2999-07-01T20:00');
    component['form'].controls.endTime.setValue('2999-07-01T23:00');
    expect(component['form'].controls.startTime.hasError('outsideEventWindow')).toBe(false);
    expect(component['form'].controls.endTime.hasError('outsideEventWindow')).toBe(false);
    expect(component['form'].controls.endTime.hasError('endBeforeStart')).toBe(false);
  });
});

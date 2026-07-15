import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ScreeningsComponent } from './screenings.component';
import { ScreeningService } from '../../../core/services/screening.service';

describe('ScreeningsComponent', () => {
  let component: ScreeningsComponent;
  let fixture: ComponentFixture<ScreeningsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScreeningsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: ScreeningService, useValue: { getByEvent: () => of([]), create: vi.fn(), update: vi.fn(), delete: () => of(void 0) } },
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

  it('accepts any future screening — the screening defines the event window, not the reverse', () => {
    component['form'].controls.screen.setValue('Screen 1');
    component['form'].controls.startTime.setValue('2999-07-01T20:00');
    component['form'].controls.endTime.setValue('2999-07-01T23:00');
    expect(component['form'].controls.startTime.hasError('notFuture')).toBe(false);
    expect(component['form'].controls.endTime.hasError('endBeforeStart')).toBe(false);
    expect(component['form'].valid).toBe(true);
  });
});

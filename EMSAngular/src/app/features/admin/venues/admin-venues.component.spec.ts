import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';

import { AdminVenuesComponent } from './admin-venues.component';
import { VenueService } from '../../../core/services/venue.service';
import { ToastService } from '../../../core/services/toast.service';

describe('AdminVenuesComponent', () => {
  let component: AdminVenuesComponent;
  let fixture: ComponentFixture<AdminVenuesComponent>;
  let venue: { list: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn>; delete: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn> };

  const fillValid = () => component['form'].setValue({
    name: 'Grand Hall', address: '12 River Road', city: 'Chennai', totalCapacity: 250,
  });

  beforeEach(() => {
    venue = {
      list: vi.fn().mockReturnValue(of([])),
      create: vi.fn().mockReturnValue(of({})),
      update: vi.fn().mockReturnValue(of({})),
      delete: vi.fn().mockReturnValue(of({})),
    };
    toast = { success: vi.fn(), error: vi.fn() };
    TestBed.configureTestingModule({
      imports: [AdminVenuesComponent],
      providers: [
        { provide: VenueService, useValue: venue },
        { provide: ToastService, useValue: toast },
      ],
    });
    fixture = TestBed.createComponent(AdminVenuesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('does not submit when the form is invalid', () => {
    component['form'].reset({ totalCapacity: 1 });
    component['save']();
    expect(venue.create).not.toHaveBeenCalled();
  });

  it('flags an over-length name against the backend 100-char cap', () => {
    component['form'].controls.name.setValue('x'.repeat(101));
    expect(component['form'].controls.name.hasError('maxlength')).toBe(true);
  });

  it('rejects a blank (whitespace-only) name against the backend non-blank rule', () => {
    component['form'].controls.name.setValue('   ');
    expect(component['form'].controls.name.hasError('notBlank')).toBe(true);
  });

  it('requires a positive capacity', () => {
    component['form'].controls.totalCapacity.setValue(0);
    expect(component['form'].controls.totalCapacity.invalid).toBe(true);
  });

  it('creates a venue and toasts success', () => {
    fillValid();
    component['save']();
    expect(venue.create).toHaveBeenCalledWith(expect.objectContaining({ name: 'Grand Hall', layoutConfig: '{}' }));
    expect(toast.success).toHaveBeenCalledWith('Venue created.');
  });

  it('confirms before deleting and toasts on success', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    component['remove'](7);
    expect(venue.delete).toHaveBeenCalledWith(7);
    expect(toast.success).toHaveBeenCalledWith('Venue deleted.');
    confirmSpy.mockRestore();
  });

  it('does not delete when the confirm is cancelled', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component['remove'](7);
    expect(venue.delete).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('preserves the existing layoutConfig when editing (no wipe to {})', () => {
    component['edit']({ id: 3, name: 'Grand Hall', address: '12 River Road', city: 'Chennai', totalCapacity: 250, layoutConfig: '{"rows":10}', createdAt: '' });
    component['save']();
    expect(venue.update).toHaveBeenCalledWith(3, expect.objectContaining({ layoutConfig: '{"rows":10}' }));
  });

  it('guards against double-submit — a second save while in flight is ignored', () => {
    // A never-completing observable keeps the first request "in flight".
    venue.create.mockReturnValue(new Observable(() => {}));
    fillValid();
    component['save']();
    component['save']();
    expect(venue.create).toHaveBeenCalledTimes(1);
  });
});

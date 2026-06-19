import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { EventFormComponent } from './event-form.component';
import { EventService } from '../../../core/services/event.service';
import { VenueService } from '../../../core/services/venue.service';

describe('EventFormComponent (create mode)', () => {
  let fixture: ComponentFixture<EventFormComponent>;
  let component: EventFormComponent;
  let eventService: { create: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    eventService = { create: vi.fn().mockReturnValue(of({ id: 9 })) };
    await TestBed.configureTestingModule({
      imports: [EventFormComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
        { provide: EventService, useValue: eventService },
        { provide: VenueService, useValue: { list: () => of([{ id: 1, name: 'Hall', address: '', city: 'X', totalCapacity: 10, layoutConfig: '', createdAt: '' }]) } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(EventFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads venues and is in create mode', () => {
    expect(component['isEdit']()).toBe(false);
    expect(component['venues']().length).toBe(1);
  });

  it('does not submit when the form is invalid', () => {
    component.submit();
    expect(eventService.create).not.toHaveBeenCalled();
  });

  it('creates an event and navigates on valid submit', () => {
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigate');
    component.form.setValue({
      venueId: 1, title: 'My Event', description: 'A great event',
      startTime: '2026-07-01T19:00', endTime: '2026-07-01T22:00',
      imageUrl: 'http://img/x.jpg', category: 'Music',
    });
    component.submit();
    expect(eventService.create).toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith(['/organizer/events']);
  });
});

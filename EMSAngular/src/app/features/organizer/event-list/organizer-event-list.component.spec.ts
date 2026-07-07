import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { OrganizerEventListComponent } from './organizer-event-list.component';
import { EventService } from '../../../core/services/event.service';

describe('OrganizerEventListComponent', () => {
  let component: OrganizerEventListComponent;
  let fixture: ComponentFixture<OrganizerEventListComponent>;
  let eventService: { getMyEvents: ReturnType<typeof vi.fn>; cancel: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    eventService = {
      getMyEvents: vi.fn().mockReturnValue(of({ items: [], totalPages: 1 })),
      cancel: vi.fn().mockReturnValue(of({ id: 3 })),
    };

    await TestBed.configureTestingModule({
      imports: [OrganizerEventListComponent],
      providers: [provideRouter([]), { provide: EventService, useValue: eventService }],
    }).compileComponents();

    fixture = TestBed.createComponent(OrganizerEventListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('opens the confirm dialog without cancelling when Cancel is clicked', () => {
    component['requestCancel'](3);
    expect(component['cancelTargetId']()).toBe(3);
    expect(eventService.cancel).not.toHaveBeenCalled();
  });

  it('cancels the event only after confirmation', () => {
    component['requestCancel'](3);
    component['confirmCancel']();
    expect(eventService.cancel).toHaveBeenCalledWith(3);
    expect(component['cancelTargetId']()).toBeNull();
  });

  it('does not cancel when the dialog is dismissed', () => {
    component['requestCancel'](3);
    component['dismissCancel']();
    expect(component['cancelTargetId']()).toBeNull();
    expect(eventService.cancel).not.toHaveBeenCalled();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EventCardComponent } from './event-card.component';
import { EventDto } from '../../../core/models/event.model';

const ev: EventDto = {
  id: 1,
  organizerId: 1,
  venueId: 1,
  title: 'Rock Night',
  description: 'desc',
  status: 'Published',
  startTime: '2026-07-01T19:00:00',
  endTime: '2026-07-01T22:00:00',
  imageUrl: 'http://img/x.jpg',
  category: 'Music',
  screen: 'Screen 1',
  slug: 'rock-night',
  createdAt: '2026-06-01T00:00:00',
};

describe('EventCardComponent', () => {
  let fixture: ComponentFixture<EventCardComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [EventCardComponent], providers: [provideRouter([])] });
    fixture = TestBed.createComponent(EventCardComponent);
    fixture.componentRef.setInput('event', ev);
    fixture.detectChanges();
  });

  it('renders the title and category', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Rock Night');
    expect(text).toContain('Music');
  });

  it('links to the event slug', () => {
    const anchor = (fixture.nativeElement as HTMLElement).querySelector('a');
    expect(anchor?.getAttribute('href')).toContain('/events/rock-night');
  });
});

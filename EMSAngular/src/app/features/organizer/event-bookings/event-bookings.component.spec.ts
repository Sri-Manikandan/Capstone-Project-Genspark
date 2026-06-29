import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { EventBookingsComponent } from './event-bookings.component';

describe('EventBookingsComponent', () => {
  let component: EventBookingsComponent;
  let fixture: ComponentFixture<EventBookingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EventBookingsComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(EventBookingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

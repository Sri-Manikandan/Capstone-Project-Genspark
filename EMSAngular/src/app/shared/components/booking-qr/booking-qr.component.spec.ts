import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BookingQrComponent } from './booking-qr.component';

describe('BookingQrComponent', () => {
  let component: BookingQrComponent;
  let fixture: ComponentFixture<BookingQrComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BookingQrComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(BookingQrComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

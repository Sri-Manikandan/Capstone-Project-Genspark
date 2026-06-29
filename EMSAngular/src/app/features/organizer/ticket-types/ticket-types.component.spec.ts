import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { TicketTypesComponent } from './ticket-types.component';

describe('TicketTypesComponent', () => {
  let component: TicketTypesComponent;
  let fixture: ComponentFixture<TicketTypesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketTypesComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(TicketTypesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

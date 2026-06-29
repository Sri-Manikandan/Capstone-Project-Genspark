import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OrganizerRequestsComponent } from './organizer-requests.component';

describe('OrganizerRequestsComponent', () => {
  let component: OrganizerRequestsComponent;
  let fixture: ComponentFixture<OrganizerRequestsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrganizerRequestsComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(OrganizerRequestsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

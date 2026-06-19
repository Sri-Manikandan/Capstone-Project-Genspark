import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  let fixture: ComponentFixture<PaginationComponent>;
  let component: PaginationComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [PaginationComponent] });
    fixture = TestBed.createComponent(PaginationComponent);
    component = fixture.componentInstance;
  });

  it('emits the next page when goTo is called', () => {
    let emitted = -1;
    component.pageChange.subscribe((p: number) => (emitted = p));
    fixture.componentRef.setInput('currentPage', 2);
    fixture.componentRef.setInput('totalPages', 5);
    component.goTo(3);
    expect(emitted).toBe(3);
  });

  it('does not emit for out-of-range pages', () => {
    let called = false;
    component.pageChange.subscribe(() => (called = true));
    fixture.componentRef.setInput('currentPage', 1);
    fixture.componentRef.setInput('totalPages', 3);
    component.goTo(0);
    component.goTo(4);
    expect(called).toBe(false);
  });
});

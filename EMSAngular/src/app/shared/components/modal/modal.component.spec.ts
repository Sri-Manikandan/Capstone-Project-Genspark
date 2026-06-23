import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { ModalComponent } from './modal.component';

@Component({
  standalone: true,
  imports: [ModalComponent],
  template: `<ems-modal [open]="true" title="Test" (closed)="onClosed()"><p>Body</p></ems-modal>`,
})
class HostComponent {
  closedCount = 0;
  onClosed(): void { this.closedCount += 1; }
}

describe('ModalComponent', () => {
  it('renders projected content and emits closed on × click', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Body');

    el.querySelector<HTMLButtonElement>('[aria-label="Close dialog"]')!.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.closedCount).toBe(1);
  });
});

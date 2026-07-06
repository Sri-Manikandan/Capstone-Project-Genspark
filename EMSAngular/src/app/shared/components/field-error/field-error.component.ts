import { Component, Input } from '@angular/core';
import { AbstractControl } from '@angular/forms';

/**
 * Renders the first validation message for a reactive-form control once it has been
 * touched or edited. Uses default change detection (not OnPush) so the message updates
 * on blur/submit even when the host control reference has not changed.
 */
@Component({
  selector: 'ems-field-error',
  standalone: true,
  template: `@if (message) {
    <span class="mt-1 block text-xs text-rose">{{ message }}</span>
  }`,
})
export class FieldErrorComponent {
  @Input({ required: true }) control!: AbstractControl | null;
  @Input() label = 'This field';

  protected get message(): string | null {
    const control = this.control;
    if (!control || !(control.touched || control.dirty) || !control.errors) return null;

    const errors = control.errors;
    if (errors['required']) return `${this.label} is required.`;
    if (errors['email']) return 'Enter a valid email address.';
    if (errors['minlength']) return `Use at least ${errors['minlength'].requiredLength} characters.`;
    if (errors['maxlength']) return `Use at most ${errors['maxlength'].requiredLength} characters.`;
    if (errors['min']) return `Enter a value of at least ${errors['min'].min}.`;
    if (errors['max']) return `Enter a value of at most ${errors['max'].max}.`;
    if (errors['mismatch']) return 'Values do not match.';
    return `${this.label} is invalid.`;
  }
}

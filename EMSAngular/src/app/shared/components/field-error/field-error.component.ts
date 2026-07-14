import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { AbstractControl } from '@angular/forms';

/**
 * Renders the first validation message for a reactive-form control once it has been
 * touched or edited.
 *
 * A control's touched/dirty/errors state is not a signal, so under this app's zoneless
 * change detection nothing would re-render this view when validity changes. Subscribing
 * to `control.events` and bumping `revision` is what makes the message reactive — without
 * it the message is computed correctly but never reaches the DOM.
 */
@Component({
  selector: 'ems-field-error',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (message()) {
    <span class="mt-1 block text-xs text-rose">{{ message() }}</span>
  }`,
})
export class FieldErrorComponent {
  readonly control = input.required<AbstractControl | null>();
  readonly label = input('This field');

  /** Bumped on every control event so `message` recomputes. */
  private readonly revision = signal(0);

  constructor() {
    effect((onCleanup) => {
      const control = this.control();
      if (!control) return;
      const sub = control.events.subscribe(() => this.revision.update((n) => n + 1));
      onCleanup(() => sub.unsubscribe());
    });
  }

  protected readonly message = computed<string | null>(() => {
    this.revision();

    const control = this.control();
    const label = this.label();
    if (!control || !(control.touched || control.dirty) || !control.errors) return null;

    const errors = control.errors;
    if (errors['required']) return `${label} is required.`;
    if (errors['notBlank']) return `${label} cannot be blank.`;
    if (errors['email']) return 'Enter a valid email address.';
    if (errors['minlength']) return `Use at least ${errors['minlength'].requiredLength} characters.`;
    if (errors['maxlength']) return `Use at most ${errors['maxlength'].requiredLength} characters.`;
    if (errors['min']) return `Enter a value of at least ${errors['min'].min}.`;
    if (errors['max']) return `Enter a value of at most ${errors['max'].max}.`;
    if (errors['url']) return 'Enter a valid http(s) URL.';
    if (errors['notFuture']) return 'Must be a future date and time.';
    if (errors['minLeadTime']) return `Must be at least ${(errors['minLeadTime'].hours ?? 48) / 24} days (${errors['minLeadTime'].hours ?? 48} hours) from now.`;
    if (errors['endBeforeStart']) return 'End time must be after the start time.';
    if (errors['saleAfterScreening']) return 'Ticket sales must end before the screening starts.';
    if (errors['outsideEventWindow']) return "Must fall within the event's start and end times.";
    if (errors['complexity']) return 'Include an uppercase, lowercase, number, and special character.';
    if (errors['pattern']) return `Enter a valid ${label.toLowerCase()}.`;
    if (errors['mismatch']) return 'Values do not match.';
    return `${label} is invalid.`;
  });
}

/**
 * The controlled vocabulary organizers pick from when creating/editing an event.
 * The backend stores `category` as a free-text string (no whitelist), so this list is
 * the single source of truth on the client. The first three match the seeded data
 * (`DataSeeder`) so existing events map cleanly onto the dropdown.
 */
export const EVENT_CATEGORIES: readonly string[] = [
  'Concerts',
  'Comedy',
  'Movies',
  'Theatre',
  'Sports',
  'Workshops',
  'Conferences',
  'Exhibitions',
  'Festivals',
  'Other',
];

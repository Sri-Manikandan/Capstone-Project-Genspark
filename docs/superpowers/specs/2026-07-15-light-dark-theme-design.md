# Light / Dark Theme + Subtle Animations — Design

**Date:** 2026-07-15
**Area:** `EMSAngular/` (Angular frontend)

## Goal

Add a user-switchable **light mode** to the app, which is currently dark-only. Dark
stays the default. Add tasteful, subtle animations. Honor `prefers-reduced-motion`.

## Decisions (from brainstorming)

- **Default theme:** always start dark; remember the user's explicit choice.
- **Toggle placement:** top navbar (desktop bar + mobile menu).
- **Animation level:** tasteful & subtle.
- **Light look:** clean paper-white — off-white backgrounds, dark ink text, same
  plum / teal / gold / rose accent family (slightly deepened for contrast on white).

## Current state

- Theming runs entirely through **semantic Tailwind tokens** defined in
  `EMSAngular/tailwind.config.js`: `ink`, `ink-soft`, `muted`, `paper`, `surface`,
  `line`, and accents `plum` / `teal` / `gold` / `rose` (each with `dark` / `tint`
  variants where used).
- Components consume tokens only (`bg-paper`, `text-ink`, `bg-surface`,
  `border-line`, `bg-paper/85`, `ring-plum/40`, `text-muted/70`, …). No `dark:`
  variants exist anywhere. No theme toggle exists.
- Hardcoded-dark values that assume a dark background live in `styles.css`
  (`boxShadow.card` / `lift` in the Tailwind config, the `.table-scroll-wrap`
  gradients, and the `.perf` notch color) — these must become theme-aware.

## Approach

**Wire the existing Tailwind color tokens to CSS variables.** This keeps every
existing utility class working untouched, including opacity utilities.

Rejected alternatives:
- Tailwind `darkMode: 'class'` + `dark:` variants — would touch every component
  template, and is inverted from the dark-default reality.
- Swapping two compiled stylesheets at runtime — heavy, causes flash-of-wrong-theme.

## Changes

### 1. Tokens → CSS variables

In `tailwind.config.js`, redefine each color as a channels-based rgb reference so
opacity utilities keep working:

```js
ink: "rgb(var(--ink) / <alpha-value>)",
paper: "rgb(var(--paper) / <alpha-value>)",
plum: {
  DEFAULT: "rgb(var(--plum) / <alpha-value>)",
  dark: "rgb(var(--plum-dark) / <alpha-value>)",
  tint: "rgb(var(--plum-tint) / <alpha-value>)",
},
// …same pattern for ink-soft, muted, surface, line, teal, gold, rose
```

Variable values are **space-separated RGB channels** (e.g. `--ink: 245 243 250;`).

In `styles.css`, add two token blocks inside `@layer base`:

- `:root` — the **current dark values** copied verbatim (no visual change to dark):
  - `--ink: 245 243 250` (#F5F3FA), `--ink-soft: 183 177 196` (#B7B1C4),
    `--muted: 138 131 152` (#8A8398), `--paper: 14 11 20` (#0E0B14),
    `--surface: 23 19 31` (#17131F), `--line: 42 36 53` (#2A2435)
  - `--plum: 139 92 246`, `--plum-dark: 124 58 237`, `--plum-tint: 33 26 56`
  - `--teal: 45 212 191`, `--teal-dark: 20 184 166`, `--teal-tint: 16 36 31`
  - `--gold: 245 158 11`, `--gold-tint: 42 32 14`
  - `--rose: 251 113 133`, `--rose-dark: 244 63 94`, `--rose-tint: 44 22 32`
- `[data-theme="light"]` — clean paper-white set:
  - `--paper: 250 249 252` (near-white, faint plum-cool tint),
    `--surface: 255 255 255`, `--line: 226 223 233`
  - `--ink: 26 22 37` (near-black ink), `--ink-soft: 74 68 90`,
    `--muted: 122 114 138`
  - Accents deepened for contrast on white: `--plum: 124 58 237`,
    `--plum-dark: 109 40 217`, `--plum-tint: 237 233 250`;
    `--teal: 13 148 136`, `--teal-dark: 15 118 110`, `--teal-tint: 224 246 243`;
    `--gold: 217 119 6`, `--gold-tint: 251 240 219`;
    `--rose: 225 29 72`, `--rose-dark: 190 18 60`, `--rose-tint: 253 232 236`
  - (Exact channel values are a starting point; tune during implementation for AA
    contrast on white.)

### 2. Theme-aware shadows and dark-assuming values

- Move `boxShadow.card` / `boxShadow.lift` to reference a variable
  (`--shadow-card`, `--shadow-lift`) defined per theme — heavy black glows for
  dark, soft neutral shadows for light.
- Replace the hardcoded `rgba(23,19,31,…)` in `.table-scroll-wrap` with
  `theme(colors.surface)` / a variable, and the `rgba(0,0,0,…)` edge fades with a
  `--table-fade` variable so light mode reads correctly.
- `.perf::before/::after` already uses `bg-paper` — verify it still punches
  correctly against `--paper` in both themes (no change expected).

### 3. `ThemeService`

New `EMSAngular/src/app/core/services/theme.service.ts`:

- Signal `theme = signal<'light' | 'dark'>(…)` initialized from `localStorage`
  key `ems-theme`, falling back to `'dark'`.
- `toggle()` flips the value, writes `document.documentElement` `data-theme`
  attribute (absent/`"dark"` for dark, `"light"` for light), persists to
  `localStorage`, and updates the `<meta name="theme-color">` content.
- Applies the current theme on construction so a programmatic set stays in sync
  with the pre-paint script (below).

### 4. Pre-paint flash guard

Small inline script in `index.html` `<head>` that reads `localStorage['ems-theme']`
and sets `data-theme` on `<html>` before first paint, so a returning light-mode
user never sees a dark flash.

### 5. Navbar toggle

In `navbar.component.ts` inject `ThemeService`; in `navbar.component.html` add a
sun/moon icon button (desktop bar and inside the mobile menu) calling
`theme.toggle()`, with `aria-label` and `aria-pressed` reflecting state.

### 6. Animations (tasteful & subtle)

- **Theme switch:** a global ~200ms `transition` on `background-color` /
  `border-color` / `color` for surfaces so the flip cross-fades instead of
  snapping.
- **Hover lift:** subtle translate/shadow lift on `.card` and buttons.
- **Content fade-in:** a gentle fade-in-up utility applied to routed page content.
- All new motion sits under the existing
  `@media (prefers-reduced-motion: reduce)` block, which already zeroes
  animations/transitions — extend it if any new selectors need explicit disabling.

## Testing

- `EMSAngular/src/app/core/services/theme.service.spec.ts`:
  - defaults to `dark` when `localStorage` is empty,
  - `toggle()` switches to `light`, sets `data-theme="light"`, persists to
    `localStorage`,
  - re-initializes from a stored value.
- Run under a non-IST zone per repo convention:
  `TZ=UTC npx ng test --watch=false` (theme logic is zone-independent, but keep
  the project's test discipline).
- Manual check: toggle on several screens (events list, booking detail, admin
  tables, forms) in both themes; confirm contrast and no dark-only artifacts.

## Out of scope

- No new dark-mode colors (dark values are copied verbatim — zero visual change).
- No component logic changes beyond the navbar button.
- No system-preference auto-switching (default is always dark per decision).
- No per-component redesigns or new pages.

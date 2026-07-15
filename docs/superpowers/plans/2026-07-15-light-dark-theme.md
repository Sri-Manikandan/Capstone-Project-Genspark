# Light / Dark Theme + Subtle Animations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a user-switchable light mode (dark stays default) to the Angular app, plus tasteful subtle animations, without touching component logic beyond a navbar toggle.

**Architecture:** Redefine the existing semantic Tailwind color tokens as `rgb(var(--token) / <alpha-value>)` so every current utility class keeps working, and declare the actual channel values per theme in `styles.css` (`:root` = dark, `[data-theme="light"]` = light). A signal-based `ThemeService` flips a `data-theme` attribute on `<html>` and persists to `localStorage`; an inline script in `index.html` applies the stored theme before first paint. Animations are CSS-only so the existing `prefers-reduced-motion` block disables them automatically.

**Tech Stack:** Angular (standalone, signals, `inject()`), Tailwind CSS, Vitest (`ng test`).

## Global Constraints

- Git commit messages: **5 words or fewer**, no body, no bullets, no co-author lines.
- Angular: use `inject()` (not constructor injection); `protected` for template-accessed members; `readonly` where applicable; kebab-case file names; component selector prefix `ems-`.
- Dark theme values must be **copied verbatim** from the current `tailwind.config.js` — zero visual change to dark mode.
- All HTTP/date logic is out of scope; theme logic is timezone-independent.
- Run tests under UTC per repo convention: `TZ=UTC npx ng test --watch=false`.
- `docs/` is gitignored — commit plan/spec files with `git add -f`.

## File Structure

- `EMSAngular/tailwind.config.js` — colors become `rgb(var(--…) / <alpha-value>)`; `boxShadow` references vars.
- `EMSAngular/src/styles.css` — new `:root` (dark) + `[data-theme="light"]` variable blocks; theme-aware shadow/table-fade vars; animation utilities.
- `EMSAngular/src/index.html` — pre-paint inline theme script.
- `EMSAngular/src/app/core/services/theme.service.ts` — new theme state service.
- `EMSAngular/src/app/core/services/theme.service.spec.ts` — unit tests.
- `EMSAngular/src/app/shared/components/navbar/navbar.component.ts` / `.html` — toggle button.

---

### Task 1: Tokenize Tailwind colors and define both theme palettes

**Files:**
- Modify: `EMSAngular/tailwind.config.js`
- Modify: `EMSAngular/src/styles.css`

**Interfaces:**
- Produces: CSS custom properties `--ink`, `--ink-soft`, `--muted`, `--paper`, `--surface`, `--line`, `--plum`, `--plum-dark`, `--plum-tint`, `--teal`, `--teal-dark`, `--teal-tint`, `--gold`, `--gold-tint`, `--rose`, `--rose-dark`, `--rose-tint`, `--shadow-card`, `--shadow-lift`, `--table-fade`. Tailwind tokens (`bg-paper`, `text-ink`, `bg-paper/85`, etc.) resolve from these. The `[data-theme="light"]` attribute on `<html>` selects the light palette.

- [ ] **Step 1: Replace the `colors` and `boxShadow` blocks in `tailwind.config.js`**

Replace the existing `colors: { … }` and `boxShadow: { … }` under `theme.extend` with:

```js
      colors: {
        ink: "rgb(var(--ink) / <alpha-value>)",
        "ink-soft": "rgb(var(--ink-soft) / <alpha-value>)",
        muted: "rgb(var(--muted) / <alpha-value>)",
        paper: "rgb(var(--paper) / <alpha-value>)",
        surface: "rgb(var(--surface) / <alpha-value>)",
        line: "rgb(var(--line) / <alpha-value>)",
        plum: {
          DEFAULT: "rgb(var(--plum) / <alpha-value>)",
          dark: "rgb(var(--plum-dark) / <alpha-value>)",
          tint: "rgb(var(--plum-tint) / <alpha-value>)",
        },
        teal: {
          DEFAULT: "rgb(var(--teal) / <alpha-value>)",
          dark: "rgb(var(--teal-dark) / <alpha-value>)",
          tint: "rgb(var(--teal-tint) / <alpha-value>)",
        },
        gold: {
          DEFAULT: "rgb(var(--gold) / <alpha-value>)",
          tint: "rgb(var(--gold-tint) / <alpha-value>)",
        },
        rose: {
          DEFAULT: "rgb(var(--rose) / <alpha-value>)",
          dark: "rgb(var(--rose-dark) / <alpha-value>)",
          tint: "rgb(var(--rose-tint) / <alpha-value>)",
        },
      },
      boxShadow: {
        card: "var(--shadow-card)",
        lift: "var(--shadow-lift)",
      },
```

Leave `fontFamily` and `letterSpacing` unchanged.

- [ ] **Step 2: Add the two palette blocks at the top of `@layer base` in `styles.css`**

Insert immediately after `@layer base {` (before the `html, body` rule):

```css
  :root {
    --ink: 245 243 250;
    --ink-soft: 183 177 196;
    --muted: 138 131 152;
    --paper: 14 11 20;
    --surface: 23 19 31;
    --line: 42 36 53;
    --plum: 139 92 246;
    --plum-dark: 124 58 237;
    --plum-tint: 33 26 56;
    --teal: 45 212 191;
    --teal-dark: 20 184 166;
    --teal-tint: 16 36 31;
    --gold: 245 158 11;
    --gold-tint: 42 32 14;
    --rose: 251 113 133;
    --rose-dark: 244 63 94;
    --rose-tint: 44 22 32;
    --shadow-card: 0 1px 2px rgba(0, 0, 0, 0.4), 0 12px 30px -18px rgba(0, 0, 0, 0.7);
    --shadow-lift: 0 22px 55px -22px rgba(0, 0, 0, 0.8);
    --table-fade: 0, 0, 0;
  }

  [data-theme="light"] {
    --ink: 26 22 37;
    --ink-soft: 74 68 90;
    --muted: 122 114 138;
    --paper: 250 249 252;
    --surface: 255 255 255;
    --line: 226 223 233;
    --plum: 124 58 237;
    --plum-dark: 109 40 217;
    --plum-tint: 237 233 250;
    --teal: 13 148 136;
    --teal-dark: 15 118 110;
    --teal-tint: 224 246 243;
    --gold: 217 119 6;
    --gold-tint: 251 240 219;
    --rose: 225 29 72;
    --rose-dark: 190 18 60;
    --rose-tint: 253 232 236;
    --shadow-card: 0 1px 2px rgba(17, 12, 34, 0.06), 0 12px 30px -18px rgba(17, 12, 34, 0.22);
    --shadow-lift: 0 22px 55px -22px rgba(17, 12, 34, 0.28);
    --table-fade: 17, 12, 34;
  }
```

- [ ] **Step 3: Make `.table-scroll-wrap` edge fades theme-aware in `styles.css`**

In the `.table-scroll-wrap` rule, replace the two `rgba(23, 19, 31, 0)` occurrences with `rgb(var(--surface) / 0)` and the two `rgba(0, 0, 0, 0.5)` / `rgba(0, 0, 0, 0)` occurrences with `rgba(var(--table-fade), 0.5)` / `rgba(var(--table-fade), 0)`. Final `background`:

```css
    background:
      linear-gradient(to right, theme(colors.surface) 30%, rgb(var(--surface) / 0)) left center,
      linear-gradient(to left, theme(colors.surface) 30%, rgb(var(--surface) / 0)) right center,
      radial-gradient(farthest-side at 0 50%, rgba(var(--table-fade), 0.5), rgba(var(--table-fade), 0)) left center,
      radial-gradient(farthest-side at 100% 50%, rgba(var(--table-fade), 0.5), rgba(var(--table-fade), 0)) right center;
```

- [ ] **Step 4: Build and verify dark mode is visually unchanged**

Run: `cd EMSAngular && npx ng build`
Expected: build succeeds with no Tailwind/CSS errors.
Then run: `npx ng serve` and load `http://localhost:4200` — with no `data-theme` set the page renders identically to before (dark). Manually set `document.documentElement.setAttribute('data-theme','light')` in devtools and confirm the whole page flips to a readable paper-white with dark text.

- [ ] **Step 5: Commit**

```bash
cd "EMSAngular" && git add tailwind.config.js src/styles.css
git commit -m "tokenize theme colors"
```

---

### Task 2: ThemeService with tests

**Files:**
- Create: `EMSAngular/src/app/core/services/theme.service.ts`
- Test: `EMSAngular/src/app/core/services/theme.service.spec.ts`

**Interfaces:**
- Produces: `ThemeService` (root-provided) with `readonly theme: Signal<'light' | 'dark'>`, `toggle(): void`, `set(theme: 'light' | 'dark'): void`. `Theme` type exported. Storage key `'ems-theme'`. On construction and on every change it sets `document.documentElement` attribute `data-theme` and updates `<meta name="theme-color">`.

- [ ] **Step 1: Write the failing test**

Create `EMSAngular/src/app/core/services/theme.service.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    TestBed.configureTestingModule({ providers: [ThemeService] });
  });

  it('defaults to dark when nothing is stored', () => {
    const service = TestBed.inject(ThemeService);
    expect(service.theme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('toggles to light, persists, and sets the attribute', () => {
    const service = TestBed.inject(ThemeService);
    service.toggle();
    expect(service.theme()).toBe('light');
    expect(localStorage.getItem('ems-theme')).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('toggles back to dark', () => {
    const service = TestBed.inject(ThemeService);
    service.toggle();
    service.toggle();
    expect(service.theme()).toBe('dark');
    expect(localStorage.getItem('ems-theme')).toBe('dark');
  });

  it('initializes from a stored value', () => {
    localStorage.setItem('ems-theme', 'light');
    const service = TestBed.inject(ThemeService);
    expect(service.theme()).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd EMSAngular && TZ=UTC npx ng test --watch=false --include='**/theme.service.spec.ts'`
Expected: FAIL — cannot resolve module `./theme.service`.

- [ ] **Step 3: Write the service**

Create `EMSAngular/src/app/core/services/theme.service.ts`:

```ts
import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'ems-theme';
const THEME_COLOR: Record<Theme, string> = {
  dark: '#0E0B14',
  light: '#FAF9FC',
};

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>(this.readStored());
  readonly theme = this._theme.asReadonly();

  constructor() {
    this.apply(this._theme());
  }

  toggle(): void {
    this.set(this._theme() === 'dark' ? 'light' : 'dark');
  }

  set(theme: Theme): void {
    this._theme.set(theme);
    localStorage.setItem(STORAGE_KEY, theme);
    this.apply(theme);
  }

  private apply(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', THEME_COLOR[theme]);
  }

  private readStored(): Theme {
    return localStorage.getItem(STORAGE_KEY) === 'light' ? 'light' : 'dark';
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd EMSAngular && TZ=UTC npx ng test --watch=false --include='**/theme.service.spec.ts'`
Expected: PASS — 4 tests green.

- [ ] **Step 5: Commit**

```bash
cd "EMSAngular" && git add src/app/core/services/theme.service.ts src/app/core/services/theme.service.spec.ts
git commit -m "add theme service"
```

---

### Task 3: Pre-paint theme script

**Files:**
- Modify: `EMSAngular/src/index.html`

**Interfaces:**
- Consumes: `localStorage['ems-theme']` written by `ThemeService`.
- Produces: `data-theme` attribute set on `<html>` before first paint, so a returning light-mode user sees no dark flash.

- [ ] **Step 1: Add the inline script in `<head>`**

In `EMSAngular/src/index.html`, immediately after the `<meta name="theme-color" content="#0E0B14">` line, add:

```html
  <script>
    (function () {
      try {
        var t = localStorage.getItem('ems-theme');
        document.documentElement.setAttribute('data-theme', t === 'light' ? 'light' : 'dark');
      } catch (e) {}
    })();
  </script>
```

- [ ] **Step 2: Verify no flash on reload in light mode**

Run: `cd EMSAngular && npx ng serve`
In the browser, toggle to light (via devtools attribute or after Task 4), then hard-reload. Expected: page paints light immediately with no dark flash.

- [ ] **Step 3: Commit**

```bash
cd "EMSAngular" && git add src/index.html
git commit -m "prevent theme flash"
```

---

### Task 4: Navbar theme toggle button

**Files:**
- Modify: `EMSAngular/src/app/shared/components/navbar/navbar.component.ts`
- Modify: `EMSAngular/src/app/shared/components/navbar/navbar.component.html`

**Interfaces:**
- Consumes: `ThemeService` from Task 2 (`theme()`, `toggle()`).

- [ ] **Step 1: Inject `ThemeService` in the component**

In `navbar.component.ts`, add the import and a protected field:

```ts
import { ThemeService } from '../../../core/services/theme.service';
```

Inside the class, after `private router = inject(Router);`, add:

```ts
  protected theme = inject(ThemeService);
```

- [ ] **Step 2: Add the toggle button to the template**

In `navbar.component.html`, inside the menu `<div>` (the one with class `absolute inset-x-0 top-full …`), add this button as the first child, before the `Events` link:

```html
          <button type="button" (click)="theme.toggle(); menuOpen.set(false)"
                  class="nav-link flex items-center gap-2 text-lg leading-none sm:mr-1"
                  [attr.aria-label]="theme.theme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'"
                  [attr.aria-pressed]="theme.theme() === 'light'">
            <span aria-hidden="true">{{ theme.theme() === 'dark' ? '☀️' : '🌙' }}</span>
            <span class="sm:hidden">{{ theme.theme() === 'dark' ? 'Light mode' : 'Dark mode' }}</span>
          </button>
```

- [ ] **Step 3: Run the navbar spec to confirm nothing broke**

Run: `cd EMSAngular && TZ=UTC npx ng test --watch=false --include='**/navbar.component.spec.ts'`
Expected: PASS (existing navbar tests still green).

- [ ] **Step 4: Manually verify the toggle**

Run: `cd EMSAngular && npx ng serve`. Click the sun/moon in the navbar (desktop and mobile menu). Expected: whole app cross-fades between dark and light; choice survives reload.

- [ ] **Step 5: Commit**

```bash
cd "EMSAngular" && git add src/app/shared/components/navbar/navbar.component.ts src/app/shared/components/navbar/navbar.component.html
git commit -m "add navbar theme toggle"
```

---

### Task 5: Subtle animations

**Files:**
- Modify: `EMSAngular/src/styles.css`

**Interfaces:**
- Consumes: `data-theme` flip (for the cross-fade) and Angular's routed-component sibling (`router-outlet + *`) for the page fade-in. Both sit above the existing `@media (prefers-reduced-motion: reduce)` block, which zeroes all animation/transition durations.

- [ ] **Step 1: Add a theme cross-fade on surfaces**

In `styles.css`, inside `@layer base` (after the `html, body` rule), add:

```css
  body,
  .card,
  .data-table,
  header,
  footer {
    transition: background-color 200ms ease, border-color 200ms ease, color 200ms ease;
  }
```

- [ ] **Step 2: Add the route fade-in**

Still inside `@layer base`, add:

```css
  @keyframes ems-fade-in-up {
    from {
      opacity: 0;
      transform: translateY(8px);
    }
    to {
      opacity: 1;
      transform: none;
    }
  }

  router-outlet + * {
    display: block;
    animation: ems-fade-in-up 260ms ease-out both;
  }
```

- [ ] **Step 3: Add a subtle button hover lift**

In `styles.css`, in the `@layer components` block, change the `.btn` rule from:

```css
  .btn {
    @apply inline-flex items-center justify-center gap-2 rounded-full px-5 py-2.5 text-sm font-semibold
           transition disabled:cursor-not-allowed disabled:opacity-50;
  }
```

to:

```css
  .btn {
    @apply inline-flex items-center justify-center gap-2 rounded-full px-5 py-2.5 text-sm font-semibold
           transition duration-200 hover:-translate-y-px active:translate-y-0
           disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:translate-y-0;
  }
```

- [ ] **Step 4: Verify reduced-motion disables the new motion**

Confirm the existing `@media (prefers-reduced-motion: reduce)` block in `@layer base` still sets `animation-duration: 0.01ms !important;` and `transition-duration: 0.01ms !important;` on `*` — it does, so the new keyframe, cross-fade, and button lift are all covered. No change needed; just confirm by reading the block.

- [ ] **Step 5: Build, then manually verify animations**

Run: `cd EMSAngular && npx ng build`
Expected: build succeeds.
Then `npx ng serve`: navigating between routes gently fades content up; toggling theme cross-fades instead of snapping; buttons lift slightly on hover. Enable "Reduce motion" in OS settings and confirm motion stops.

- [ ] **Step 6: Commit**

```bash
cd "EMSAngular" && git add src/styles.css
git commit -m "add subtle theme animations"
```

---

## Final verification

- [ ] Run the full suite under UTC: `cd EMSAngular && TZ=UTC npx ng test --watch=false` — all green.
- [ ] `cd EMSAngular && npx ng build` succeeds.
- [ ] Manual sweep in both themes across events list, event detail, booking detail, admin tables, and a form: text readable, borders/surfaces correct, no dark-only artifacts (heavy shadows, invisible table fades).

## Self-Review Notes

- **Spec coverage:** tokens→vars (Task 1), light/dark palettes + theme-aware shadows/table fade (Task 1), ThemeService + persistence + meta (Task 2), pre-paint guard (Task 3), navbar toggle desktop+mobile (Task 4), theme cross-fade + route fade-in + hover lift + reduced-motion (Task 5), tests (Task 2). All spec sections mapped.
- **Note vs spec:** spec listed `.perf` as "verify only" — it already uses `bg-paper`, which now resolves per-theme automatically, so no task is needed; the notch punches correctly in both themes.
- **Type consistency:** `ThemeService.theme` / `toggle()` / `set()` and the `Theme` type are used identically in Tasks 2 and 4. Storage key `'ems-theme'` matches across the service (Task 2) and the pre-paint script (Task 3).

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

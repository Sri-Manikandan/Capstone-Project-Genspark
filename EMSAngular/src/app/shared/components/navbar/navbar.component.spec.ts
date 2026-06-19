import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { NavbarComponent } from './navbar.component';
import { AuthService } from '../../../core/services/auth.service';

describe('NavbarComponent', () => {
  let fixture: ComponentFixture<NavbarComponent>;
  let auth: any;

  function setup() {
    TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    fixture = TestBed.createComponent(NavbarComponent);
    fixture.detectChanges();
  }

  it('shows login link when unauthenticated', () => {
    auth = { isAuthenticated: signal(false), role: signal(null), currentUser: signal(null), logout: () => {} };
    setup();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Login');
  });

  it('shows Admin link for admin role', () => {
    auth = {
      isAuthenticated: signal(true), role: signal('Admin'),
      currentUser: signal({ name: 'Boss' }), logout: () => {},
    };
    setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Admin');
  });
});

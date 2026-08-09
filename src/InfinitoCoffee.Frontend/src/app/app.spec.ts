import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthenticationState } from './core/auth/authentication-state.service';
import { AuthenticationService } from './core/auth/authentication.service';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        AuthenticationState,
        { provide: AuthenticationService, useValue: { logout: () => Promise.resolve() } },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render shell navigation', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Operación en tiempo real');
    expect(compiled.textContent).toContain('Pickup');
    expect(compiled.textContent).toContain('Iniciar sesión');
  });

  it('renders administrator navigation and session identity', async () => {
    const state = TestBed.inject(AuthenticationState);
    state.setUser({
      id: 'user-1',
      username: 'admin',
      displayName: 'Administrador',
      role: 'Administrator',
    });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Administrador');
    expect(text).toContain('Cocina');
    expect(text).toContain('Nueva orden');
    expect(text).not.toContain('Iniciar sesión');
  });
});

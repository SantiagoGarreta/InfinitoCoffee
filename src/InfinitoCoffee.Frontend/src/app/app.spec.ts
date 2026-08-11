import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthenticationState } from './core/auth/authentication-state.service';
import { AuthenticationService } from './core/auth/authentication.service';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
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

it('uses Angular route paths for shell navigation links', async () => {
  const state = TestBed.inject(AuthenticationState);
  state.setUser({
    id: 'user-1',
    username: 'admin',
    displayName: 'Administrador',
    role: 'Administrator',
  });

  const fixture = TestBed.createComponent(App);
  fixture.detectChanges();
  await fixture.whenStable();

  const links = getNavigationLinks(fixture.nativeElement as HTMLElement);

  expect(links.Cocina.getAttribute('href')).toBe('/kitchen');
  expect(links.Pickup.getAttribute('href')).toBe('/pickup');
  expect(links['Nueva orden'].getAttribute('href')).toBe('/orders/new');
});

it('navigates from /kitchen to Pickup without changing origin or port', async () => {
  const state = TestBed.inject(AuthenticationState);
  state.setUser({
    id: 'user-1',
    username: 'admin',
    displayName: 'Administrador',
    role: 'Administrator',
  });

  const router = TestBed.inject(Router);
  const fixture = TestBed.createComponent(App);

  await router.navigateByUrl('/kitchen');
  fixture.detectChanges();
  await fixture.whenStable();

  const pickupLink = getNavigationLinks(
    fixture.nativeElement as HTMLElement
  ).Pickup;

  const pickupHref = pickupLink.getAttribute('href');

  expect(pickupHref).toBe('/pickup');
  expect(resolveHref('http://localhost:4200/kitchen', pickupHref))
    .toBe('http://localhost:4200/pickup');

  pickupLink.click();
  fixture.detectChanges();
  await fixture.whenStable();

  expect(router.url).toBe('/pickup');
});

 it('navigates from /pickup to Nueva orden without changing origin or port', async () => {
  const state = TestBed.inject(AuthenticationState);
  state.setUser({
    id: 'user-1',
    username: 'admin',
    displayName: 'Administrador',
    role: 'Administrator',
  });

  const router = TestBed.inject(Router);
  const fixture = TestBed.createComponent(App);

  await router.navigateByUrl('/pickup');
  fixture.detectChanges();
  await fixture.whenStable();

  const newOrderLink = getNavigationLinks(
    fixture.nativeElement as HTMLElement
  )['Nueva orden'];

  const newOrderHref = newOrderLink.getAttribute('href');

  expect(newOrderHref).toBe('/orders/new');
  expect(resolveHref('http://localhost:4200/pickup', newOrderHref))
    .toBe('http://localhost:4200/orders/new');

  newOrderLink.click();
  fixture.detectChanges();
  await fixture.whenStable();

  expect(router.url).toBe('/orders/new');
});;
});

function getNavigationLinks(
  container: HTMLElement
): Record<'Cocina' | 'Pickup' | 'Nueva orden', HTMLAnchorElement> {
  const anchors = [...container.querySelectorAll('a.tabs__link')] as HTMLAnchorElement[];

  return {
    Cocina: getLinkByLabel(anchors, 'Cocina'),
    Pickup: getLinkByLabel(anchors, 'Pickup'),
    'Nueva orden': getLinkByLabel(anchors, 'Nueva orden'),
  };
}

function getLinkByLabel(anchors: HTMLAnchorElement[], label: string): HTMLAnchorElement {
  const link = anchors.find((anchor) => anchor.textContent?.trim() === label);

  expect(link).toBeDefined();

  return link as HTMLAnchorElement;
}

function resolveHref(currentUrl: string, href: string | null): string {
  expect(href).not.toBeNull();

  return new URL(href as string, currentUrl).toString();
}

import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes)],
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
    expect(compiled.querySelector('h1')?.textContent).toContain('Tiempo real para cocina y pickup');
    expect(compiled.textContent).toContain('Kitchen');
    expect(compiled.textContent).toContain('Pickup');
    expect(compiled.textContent).toContain('Caja');
  });

  it('uses Angular route paths for shell navigation links', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const links = getNavigationLinks(fixture.nativeElement as HTMLElement);

    expect(links.Kitchen.getAttribute('href')).toBe('/kitchen');
    expect(links.Pickup.getAttribute('href')).toBe('/pickup');
    expect(links.Caja.getAttribute('href')).toBe('/orders/new');
  });

  it('navigates from /kitchen to Pickup without changing origin or port', async () => {
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(App);

    await router.navigateByUrl('/kitchen');
    fixture.detectChanges();
    await fixture.whenStable();

    const pickupLink = getNavigationLinks(fixture.nativeElement as HTMLElement).Pickup;
    const pickupHref = pickupLink.getAttribute('href');

    expect(pickupHref).toBe('/pickup');
    expect(resolveHref('http://localhost:4200/kitchen', pickupHref)).toBe('http://localhost:4200/pickup');

    pickupLink.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(router.url).toBe('/pickup');
  });

  it('navigates from /pickup to Caja without changing origin or port', async () => {
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(App);

    await router.navigateByUrl('/pickup');
    fixture.detectChanges();
    await fixture.whenStable();

    const cajaLink = getNavigationLinks(fixture.nativeElement as HTMLElement).Caja;
    const cajaHref = cajaLink.getAttribute('href');

    expect(cajaHref).toBe('/orders/new');
    expect(resolveHref('http://localhost:4200/pickup', cajaHref)).toBe('http://localhost:4200/orders/new');

    cajaLink.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(router.url).toBe('/orders/new');
  });
});

function getNavigationLinks(container: HTMLElement): Record<'Kitchen' | 'Pickup' | 'Caja', HTMLAnchorElement> {
  const anchors = [...container.querySelectorAll('a.tabs__link')] as HTMLAnchorElement[];

  return {
    Kitchen: getLinkByLabel(anchors, 'Kitchen'),
    Pickup: getLinkByLabel(anchors, 'Pickup'),
    Caja: getLinkByLabel(anchors, 'Caja'),
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

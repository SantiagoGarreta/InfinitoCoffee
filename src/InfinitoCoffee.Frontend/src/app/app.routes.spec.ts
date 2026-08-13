import { RenderMode } from '@angular/ssr';

import { routes } from './app.routes';
import { serverRoutes } from './app.routes.server';

describe('application route structure', () => {
  it('keeps pickup public and outside the authenticated shell', () => {
    const pickup = routes.find((route) => route.path === 'pickup');
    const shell = routes.find((route) => route.path === '' && route.children);

    expect(pickup).toBeDefined();
    expect(pickup?.canActivate).toBeUndefined();
    expect(shell?.children?.some((route) => route.path === 'pickup')).toBe(false);
  });

  it('places operational and administrator routes under one authenticated shell', () => {
    const shell = routes.find((route) => route.path === '' && route.children);
    expect(shell?.canActivate?.length).toBe(1);
    expect(shell?.children?.map((route) => route.path)).toEqual(['orders/new', 'kitchen', 'admin']);

    const admin = shell?.children?.find((route) => route.path === 'admin');
    expect(admin?.canActivate?.length).toBe(1);
    expect(admin?.children?.map((route) => route.path)).toEqual(['', 'products', 'categories', 'users']);
  });

  it('keeps public routes prerendered and every private route client-rendered', () => {
    expect(serverRoutes.find((route) => route.path === 'login')?.renderMode).toBe(RenderMode.Prerender);
    expect(serverRoutes.find((route) => route.path === 'pickup')?.renderMode).toBe(RenderMode.Prerender);
    expect(serverRoutes.find((route) => route.path === '**')?.renderMode).toBe(RenderMode.Client);
  });
});

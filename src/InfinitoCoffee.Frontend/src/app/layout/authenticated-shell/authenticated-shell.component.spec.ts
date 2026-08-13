import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterOutlet } from '@angular/router';

import { AuthenticationState } from '../../core/auth/authentication-state.service';
import { AuthenticationService } from '../../core/auth/authentication.service';
import { AuthenticatedShellComponent } from './authenticated-shell.component';

@Component({ standalone: true, template: '<p class="page-a">Página A</p>' })
class PageA {}
@Component({ standalone: true, template: '<p class="page-b">Página B</p>' })
class PageB {}
@Component({ standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class TestRoot {}

describe('AuthenticatedShellComponent', () => {
  it('keeps exactly one sidebar and the same DOM element between private child routes', async () => {
    await TestBed.configureTestingModule({
      imports: [TestRoot],
      providers: [
        AuthenticationState,
        { provide: AuthenticationService, useValue: { logout: () => Promise.resolve() } },
        provideRouter([{ path: '', component: AuthenticatedShellComponent, children: [
          { path: 'a', component: PageA }, { path: 'b', component: PageB },
        ] }]),
      ],
    }).compileComponents();
    TestBed.inject(AuthenticationState).setUser({ id: '1', username: 'admin', displayName: 'Admin', role: 'Administrator' });
    const root = TestBed.createComponent(TestRoot);
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/a');
    root.detectChanges();
    await root.whenStable();
    const firstSidebar = root.nativeElement.querySelector('app-authenticated-sidebar');
    expect(root.nativeElement.querySelectorAll('app-authenticated-sidebar')).toHaveLength(1);
    expect(root.nativeElement.querySelectorAll('nav[aria-label="Navegación principal"]')).toHaveLength(1);
    expect(root.nativeElement.querySelector('.page-a')).not.toBeNull();

    await router.navigateByUrl('/b');
    root.detectChanges();
    await root.whenStable();
    expect(root.nativeElement.querySelector('app-authenticated-sidebar')).toBe(firstSidebar);
    expect(root.nativeElement.querySelectorAll('app-authenticated-sidebar')).toHaveLength(1);
    expect(root.nativeElement.querySelector('.page-b')).not.toBeNull();
  });
});

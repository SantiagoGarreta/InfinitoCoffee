import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';

import { AdminPlaceholderPageComponent } from './admin-placeholder-page.component';

describe('AdminPlaceholderPageComponent', () => {
  it.each(['Productos', 'Categorías', 'Usuarios'])('uses route data for %s', async (title) => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminPlaceholderPageComponent],
      providers: [provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { data: { title } } } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(AdminPlaceholderPageComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain(title);
    expect(fixture.nativeElement.textContent).toContain('Esta sección se implementará próximamente');
  });
});

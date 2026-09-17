import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthenticationService, InvalidCredentialsError } from '../../../../core/auth/authentication.service';
import { LoginPageComponent } from './login-page.component';

describe('LoginPageComponent', () => {
  const authenticationService = {
    login: vi.fn(),
  };

  beforeEach(async () => {
    authenticationService.login.mockReset();
    await TestBed.configureTestingModule({
      imports: [LoginPageComponent],
      providers: [
        provideRouter([]),
        { provide: AuthenticationService, useValue: authenticationService },
      ],
    }).compileComponents();
  });

  it('uses a generic message and clears password after invalid credentials', async () => {
    authenticationService.login.mockRejectedValue(new InvalidCredentialsError());
    const fixture = TestBed.createComponent(LoginPageComponent);
    const component = fixture.componentInstance;
    component.form.setValue({ username: 'unknown', password: 'secret' });

    await component.submit();
    fixture.detectChanges();

    expect(authenticationService.login).toHaveBeenCalledWith('unknown', 'secret');
    expect(component.form.controls.password.value).toBe('');
    expect(fixture.nativeElement.textContent).toContain('Usuario o contraseña incorrectos.');
  });
});

import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { getRoleHome } from '../../../../core/auth/auth-navigation';
import { AuthenticationService, InvalidCredentialsError } from '../../../../core/auth/authentication.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent {
  private readonly authenticationService = inject(AuthenticationService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly form = new FormGroup({
    username: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    try {
      const user = await this.authenticationService.login(
        this.form.controls.username.value,
        this.form.controls.password.value,
      );
      await this.router.navigate([getRoleHome(user.role)], { replaceUrl: true });
    } catch (error: unknown) {
      if (error instanceof InvalidCredentialsError) {
        this.errorMessage.set('Usuario o contraseña incorrectos.');
      } else if (error instanceof HttpErrorResponse && error.status === 0) {
        this.errorMessage.set('No fue posible conectar con la API. Intentá nuevamente.');
      } else {
        this.errorMessage.set('No fue posible iniciar sesión. Intentá nuevamente.');
      }
    } finally {
      this.form.controls.password.reset('');
      this.submitting.set(false);
    }
  }
}

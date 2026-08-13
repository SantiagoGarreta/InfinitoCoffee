import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthenticationState } from '../../../core/auth/authentication-state.service';

@Component({
  selector: 'app-admin-home-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './admin-home-page.component.html',
  styleUrl: './admin-home-page.component.scss',
})
export class AdminHomePageComponent {
  readonly currentUser = inject(AuthenticationState).currentUser;
}

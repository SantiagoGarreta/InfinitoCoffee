import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { AuthenticatedSidebarComponent } from '../authenticated-sidebar/authenticated-sidebar.component';

@Component({
  selector: 'app-authenticated-shell',
  standalone: true,
  imports: [AuthenticatedSidebarComponent, RouterOutlet],
  templateUrl: './authenticated-shell.component.html',
  styleUrl: './authenticated-shell.component.scss',
})
export class AuthenticatedShellComponent {}

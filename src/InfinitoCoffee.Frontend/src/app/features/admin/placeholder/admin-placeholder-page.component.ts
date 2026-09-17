import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-admin-placeholder-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './admin-placeholder-page.component.html',
  styleUrl: './admin-placeholder-page.component.scss',
})
export class AdminPlaceholderPageComponent {
  readonly title = inject(ActivatedRoute).snapshot.data['title'] as string;
}

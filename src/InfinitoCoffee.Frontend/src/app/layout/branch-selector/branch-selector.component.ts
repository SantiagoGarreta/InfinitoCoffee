import { isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, inject, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BranchState } from '../../core/branches/branch-state.service';

@Component({
  selector: 'app-branch-selector',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (pickup() || state.isAdministrator()) {
      <select aria-label="Sucursal" [ngModel]="currentId()" (ngModelChange)="change($event)">
        @for (branch of state.branches(); track branch.id) {
          <option [ngValue]="branch.id">{{ branch.name }}</option>
        }
      </select>
    } @else {
      <span class="assigned-branch">{{ state.name(currentId()) }}</span>
    }
  `,
  styles: [`
    :host { display: inline-block; max-width: 100%; }
    :host(.sidebar-mode) { display: block; }
    select, .assigned-branch {
      box-sizing: border-box;
      max-width: 100%;
      padding: .45rem .65rem;
      border: 1px solid #c9b9a5;
      border-radius: .55rem;
      background: #fffaf4;
      color: #36271d;
      font: inherit;
      font-size: .875rem;
    }
    select { cursor: pointer; }
    select:focus-visible { outline: 3px solid #e8bd79; outline-offset: 2px; }
    .assigned-branch { display: block; }
    :host(.sidebar-mode) select, :host(.sidebar-mode) .assigned-branch { width: 100%; }
  `],
  host: { '[class.sidebar-mode]': 'sidebar()' },
})
export class BranchSelectorComponent {
  readonly state = inject(BranchState);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly pickup = input(false);
  readonly sidebar = input(false);

  currentId(): number { return this.pickup() ? this.state.publicId() : this.state.privateId(); }

  change(id: number): void {
    if (!this.isBrowser || id === this.currentId()) return;
    if (this.pickup()) {
      const url = new URL(window.location.href);
      url.searchParams.set('branchId', String(id));
      window.location.assign(url.toString());
    } else {
      this.state.selectPrivate(id);
      // Recreate stores and connections together; no old branch data survives the switch.
      window.location.reload();
    }
  }
}

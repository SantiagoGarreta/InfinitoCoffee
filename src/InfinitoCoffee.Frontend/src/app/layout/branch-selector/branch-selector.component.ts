import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BranchState } from '../../core/branches/branch-state.service';
import { BranchesApiService } from '../../core/branches/branches-api.service';
import { toUserMessage } from '../../core/http/api-error.utils';

@Component({
  selector: 'app-branch-selector', standalone: true, imports: [FormsModule],
  template: `
    <div class="branch-toolbar" [class.branch-toolbar--sidebar]="sidebar()">
      @if (pickup() || state.isAdministrator()) {
        <label>{{ sidebar() ? 'Sucursal activa' : 'Sucursal' }}
          <select aria-label="Sucursal activa" [ngModel]="currentId()" (ngModelChange)="change($event)" [disabled]="busy()">
            @for (branch of state.branches(); track branch.id) { <option [ngValue]="branch.id">{{ branch.name }}</option> }
          </select>
        </label>
      } @else { <div class="assigned-branch"><span>Sucursal asignada</span><strong>{{ state.name(currentId()) }}</strong></div> }
      @if (!pickup() && state.isAdministrator()) {
        <button type="button" (click)="beginRename()" [disabled]="busy()">Cambiar nombre</button>
        @if (!sidebar()) { <a [href]="'/pickup?branchId=' + currentId()" target="_blank" rel="noopener">Abrir pickup de esta sucursal ↗</a> }
      }
      @if (editing()) {
        <form (ngSubmit)="rename()">
          <label>Nombre de la sucursal <input name="branchName" required maxlength="100" [(ngModel)]="name" [disabled]="busy()" /></label>
          <button type="submit" [disabled]="busy() || !name.trim()">Guardar</button>
          <button type="button" (click)="editing.set(false)" [disabled]="busy()">Cancelar</button>
        </form>
      }
      @if (error()) { <p role="alert">{{ error() }} <button type="button" (click)="load()">Reintentar</button></p> }
    </div>`,
  styles: [`
    :host { display: block; margin-bottom: 1.25rem; }
    :host(.sidebar-mode) { margin: 0; }
    .branch-toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: .75rem; padding: .85rem 1rem;
      border: 1px solid #dfd5c9; border-radius: 12px; background: #fffaf4; color: #36271d; }
    label { display: flex; align-items: center; gap: .6rem; font-weight: 600; }
    select, input, button { font: inherit; padding: .4rem .6rem; border: 1px solid #c9b9a5; border-radius: 6px; background: white; color: inherit; }
    button { cursor: pointer; } a { color: #69472f; } form { display: flex; flex-wrap: wrap; gap: .5rem; width: 100%; }
    p { margin: 0; color: #912e26; } @media (max-width: 600px) { label, select { max-width: 100%; } }
    .assigned-branch { display: grid; gap: .25rem; }
    .assigned-branch span { font-size: .78rem; }
    .branch-toolbar--sidebar { display: grid; gap: .5rem; padding: .75rem; }
    .branch-toolbar--sidebar label { display: grid; gap: .35rem; }
    .branch-toolbar--sidebar select { width: 100%; min-width: 0; }
    .branch-toolbar--sidebar button { justify-self: start; }
  `],
  host: { '[class.sidebar-mode]': 'sidebar()' },
})
export class BranchSelectorComponent implements OnInit {
  readonly state = inject(BranchState);
  private readonly api = inject(BranchesApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly pickup = input(false);
  readonly sidebar = input(false);
  readonly editing = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  name = '';
  currentId(): number { return this.pickup() ? this.state.publicId() : this.state.privateId(); }
  ngOnInit(): void { if (this.isBrowser) void this.load(); }
  async load(): Promise<void> {
    this.error.set(null);
    try { this.state.branches.set(await this.api.getAll()); }
    catch (error) { this.error.set(toUserMessage(error, 'No fue posible cargar las sucursales.')); }
  }
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
  beginRename(): void { this.name = this.state.name(this.currentId()); this.editing.set(true); }
  async rename(): Promise<void> {
    if (this.busy() || !this.name.trim()) return;
    this.busy.set(true); this.error.set(null);
    try {
      const branch = await this.api.rename(this.currentId(), this.name.trim());
      this.state.branches.update(list => list.map(x => x.id === branch.id ? branch : x));
      this.editing.set(false);
    } catch (error) { this.error.set(toUserMessage(error, 'No fue posible guardar el nombre.')); }
    finally { this.busy.set(false); }
  }
}

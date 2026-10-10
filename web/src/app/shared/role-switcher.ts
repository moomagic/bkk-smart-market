import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Api, Session } from '../core/api';
import { DEMO_ROLES, DemoRole, DemoSession } from '../core/demo-session';

/** Top-right role switcher. Shows who the API says we are, to prove the headers arrive. */
@Component({
  selector: 'app-role-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="switcher">
      <span class="label">บทบาท</span>
      <select [value]="session.role()" (change)="switch($any($event.target).value)">
        @for (role of roles; track role) {
          <option [value]="role">{{ labels[role].label }}</option>
        }
      </select>
    </label>
    @if (confirmed(); as s) {
      <span class="who mono" title="ตัวตนที่ API ได้รับ">{{ s.role }} · {{ s.user }}</span>
    }
  `,
  styles: `
    :host { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; justify-content: flex-end; }
    .switcher { display: flex; align-items: center; gap: 6px; }
    .label { color: var(--muted); font-size: 0.8rem; }
    select {
      font: inherit;
      padding: 4px 8px;
      border: 1px solid var(--line);
      border-radius: 4px;
      background: var(--surface);
      color: var(--ink);
    }
    .who { color: var(--muted); font-size: 0.7rem; max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  `,
})
export class RoleSwitcher {
  protected readonly session = inject(DemoSession);
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  protected readonly roles = Object.keys(DEMO_ROLES) as DemoRole[];
  protected readonly labels = DEMO_ROLES;
  protected readonly confirmed = signal<Session | null>(null);

  constructor() {
    effect(() => {
      this.session.role();
      this.api.session().subscribe({
        next: (s) => this.confirmed.set(s),
        error: () => this.confirmed.set(null),
      });
    });
  }

  protected switch(role: DemoRole): void {
    this.session.switchTo(role);
    void this.router.navigateByUrl(DEMO_ROLES[role].home);
  }
}

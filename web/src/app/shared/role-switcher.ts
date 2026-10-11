import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Select } from 'primeng/select';
import { Api } from '../core/api';
import { DEMO_ROLES, DemoRole, DemoSession } from '../core/demo-session';

/** Top-right role switcher. Who the API says we are (proof the demo headers arrive) is in its tooltip. */
@Component({
  selector: 'app-role-switcher',
  imports: [FormsModule, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="label" id="role-switcher-label">บทบาท</span>
    <!-- Bound to the session signal, so a reload shows the stored role (the native select once showed the first).
         On body, the panel is kept inside the viewport; inline it overflowed the right edge at 390px. -->
    <p-select
      ariaLabelledBy="role-switcher-label"
      size="small"
      appendTo="body"
      [options]="options"
      optionLabel="label"
      optionValue="role"
      [ngModel]="session.role()"
      (ngModelChange)="switch($event)"
    />

  `,
  // Who the API says we are, kept as a tooltip and data attributes rather than raw ids in the header
  host: {
    '[attr.title]': "confirmed() ? 'ตัวตนที่ API ได้รับ: ' + confirmed()!.role + ' · ' + confirmed()!.user : null",
    '[attr.data-confirmed-role]': 'confirmed()?.role',
    '[attr.data-confirmed-user]': 'confirmed()?.user',
  },
  styles: `
    :host { display: flex; align-items: center; gap: var(--space-2); flex-wrap: wrap; justify-content: flex-end; }
    .label { color: var(--muted); font-size: var(--text-small); }
    /* Phone: the select alone; its name stays for screen readers, the API identity echo is a desktop detail */
    @media (max-width: 640px) {
      :host { flex-wrap: nowrap; }
      .label { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
    }
  `,
})
export class RoleSwitcher {
  protected readonly session = inject(DemoSession);
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  protected readonly options = (Object.keys(DEMO_ROLES) as DemoRole[]).map((role) => ({
    role,
    label: DEMO_ROLES[role].label,
  }));

  /** Identity echoed by the API; switchMap drops stale responses when roles change quickly. */
  protected readonly confirmed = toSignal(
    toObservable(this.session.role).pipe(
      switchMap(() => this.api.session().pipe(catchError(() => of(null)))),
    ),
    { initialValue: null },
  );

  protected switch(role: DemoRole): void {
    this.session.switchTo(role);
    void this.router.navigateByUrl(DEMO_ROLES[role].home);
  }
}

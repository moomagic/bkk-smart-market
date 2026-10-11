import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Select } from 'primeng/select';
import { Api } from '../core/api';
import { DEMO_ROLES, DemoRole, DemoSession } from '../core/demo-session';

/** Top-right role switcher. Shows who the API says we are, to prove the headers arrive. */
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
    @if (confirmed(); as s) {
      <span class="who mono" title="ตัวตนที่ API ได้รับ">{{ s.role }} · {{ s.user }}</span>
    }
  `,
  styles: `
    :host { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; justify-content: flex-end; }
    .label { color: var(--muted); font-size: 0.8rem; }
    .who { color: var(--muted); font-size: 0.7rem; max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
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

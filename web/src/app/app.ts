import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MockBadge } from './shared/mock-badge';
import { RoleSwitcher } from './shared/role-switcher';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MockBadge, RoleSwitcher],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styles: `
    .bar { background: var(--surface); border-bottom: 1px solid var(--line); }
    .bar-inner { max-width: 1280px; margin: 0 auto; padding: 10px 16px; display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .brand { font-family: var(--font-heading); font-weight: 600; color: var(--ink); text-decoration: none; }
    nav { display: flex; gap: 12px; }
    nav a { color: var(--muted); text-decoration: none; font-size: 0.9rem; }
    nav a.active { color: var(--accent); font-weight: 600; }
    app-role-switcher, .role-placeholder { margin-left: auto; }
    .role-placeholder { min-width: 150px; min-height: 32px; font-size: 0.8rem; }
    main { max-width: 1280px; margin: 0 auto; padding: 16px; }
    /* Phone: one row — brand, the mock-data badge (Guardrail 5) and the role select, which also switches area */
    @media (max-width: 640px) {
      .bar-inner { flex-wrap: nowrap; gap: 8px; padding: 8px 16px; }
      .brand { font-size: 0.95rem; white-space: nowrap; }
      nav { display: none; }
      .role-placeholder { min-width: 120px; }
    }
  `,
})
export class App {}

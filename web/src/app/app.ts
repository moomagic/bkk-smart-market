import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { MockBadge } from './shared/mock-badge';
import { RoleSwitcher } from './shared/role-switcher';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, MockBadge, RoleSwitcher],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styles: `
    .bar { background: var(--surface); border-bottom: 1px solid var(--line); }
    .bar-inner { max-width: 1280px; margin: 0 auto; padding: var(--space-3) var(--space-4); display: flex; align-items: center; gap: var(--space-3); flex-wrap: wrap; }
    .brand { font-family: var(--font-heading); font-weight: 600; color: var(--ink); text-decoration: none; }
    app-role-switcher, .role-placeholder { margin-left: auto; }
    .role-placeholder { min-width: 150px; min-height: 32px; font-size: var(--text-small); }
    main { max-width: 1280px; margin: 0 auto; padding: var(--space-4); }
    main.bare { max-width: none; padding: 0; }
    /* One row: brand, the mock-data badge (Guardrail 5) and the role select, which also switches area */
    @media (max-width: 640px) {
      .bar-inner { flex-wrap: nowrap; gap: var(--space-2); padding: var(--space-2) var(--space-4); }
      .brand { font-size: var(--text-body); white-space: nowrap; }
      .role-placeholder { min-width: 120px; }
    }
  `,
})
export class App {
  private readonly router = inject(Router);

  /** Inside /vendor the phone app shell replaces the site header and page padding. */
  protected readonly inVendor = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map((e) => isVendorUrl(e.urlAfterRedirects)),
    ),
    // Before the first navigation: everything but the portal and dashboard redirects into /vendor
    { initialValue: !/^\/(portal|dashboard)(\/|$)/.test(location.pathname) },
  );
}

function isVendorUrl(url: string): boolean {
  return url === '/vendor' || url.startsWith('/vendor/') || url.startsWith('/vendor?');
}

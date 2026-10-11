import { ChangeDetectionStrategy, Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, NavigationStart, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { Location } from '@angular/common';
import { Message } from 'primeng/message';
import { MockBadge } from '../shared/mock-badge';
import { RoleSwitcher } from '../shared/role-switcher';
import { VendorStore } from './vendor-store';

/** The bottom tabs, in the order a vendor meets them. */
const TABS = [
  { path: 'requests', label: 'คำขอ' },
  { path: 'spaces', label: 'หาพื้นที่' },
  { path: 'assistant', label: 'ถามระเบียบ' },
] as const;

/**
 * The vendor area as a phone app: an app bar naming the screen, the screen itself scrolling under it, and the
 * tabs at the bottom within the thumb's reach. Wider than a phone, it sits in a 390px frame in the middle of the
 * screen, so a presenter on a projector shows a phone app rather than a stretched page.
 */
@Component({
  selector: 'app-vendor-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Message, MockBadge, RoleSwitcher],
  providers: [VendorStore],
  template: `
    <header class="appbar">
      @if (screen().back) {
        <button type="button" class="back" aria-label="ย้อนกลับ" (click)="back()">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
        </button>
      }
      <h1>{{ screen().heading }}</h1>
      <app-mock-badge />
      <!-- A presenter's control, dressed like the mock-data stamp so a vendor never takes it for part of the app -->
      <button type="button" class="demo" aria-label="เมนูเดโม" aria-controls="vendor-demo" [attr.aria-expanded]="demoOpen()"
        (click)="demoOpen.set(!demoOpen())">เดโม</button>
    </header>
    @if (demoOpen()) {
      <div id="vendor-demo" class="demo-panel">
        <!-- Deferred as in the site header: PrimeNG Select stays out of the initial bundle -->
        @defer (on immediate) {
          <app-role-switcher />
        } @placeholder {
          <span class="muted text-small">กำลังโหลด…</span>
        }
      </div>
    }

    <!-- One live region that is always there; the message inside is presentational, so a notice that is still
         animating out never makes a second status (or alert) -->
    <div role="status" class="notices">
      @if (store.notice(); as n) {
        <p-message class="notice" role="presentation" aria-live="off" [severity]="n.kind === 'ok' ? 'info' : n.kind">{{ n.text }}</p-message>
      }
    </div>

    <!-- The app root keeps the page's one <main>; this is the scrolling screen inside it -->
    <div #scroller class="screen">
      <router-outlet />
    </div>

    <!-- A screen with ← has its own actions at the bottom in place of the tabs -->
    @if (!screen().back) {
      <nav class="tabs" aria-label="เมนูหลัก">
        @for (tab of tabs; track tab.path) {
            <a [routerLink]="tab.path" routerLinkActive="active" ariaCurrentWhenActive="page">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              @switch (tab.path) {
                @case ('requests') {
                  <path d="M7 3h7l5 5v13H7z" /><path d="M14 3v5h5" /><path d="M10 13h6M10 17h6" />
                }
                @case ('spaces') {
                  <path d="M12 21s-6-5.6-6-11a6 6 0 0 1 12 0c0 5.4-6 11-6 11z" /><circle cx="12" cy="10" r="2.2" />
                }
                @case ('assistant') {
                  <path d="M4 5h16v11H9l-5 4z" /><path d="M8 9.5h8M8 12.5h5" />
                }
              }
            </svg>
            <span>{{ tab.label }}</span>
          </a>
        }
      </nav>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100vh;
      height: 100dvh;
      background: var(--ground);
    }
    /* Wider than a phone: the app keeps a phone's shape in the middle of the screen */
    @media (min-width: 481px) {
      :host {
        width: 390px;
        height: calc(100dvh - 2 * var(--space-5));
        max-height: 900px;
        margin: var(--space-5) auto;
        border: 1px solid var(--line);
        border-radius: 28px;
        overflow: hidden;
        box-shadow: 0 12px 40px rgb(23 32 29 / 0.14);
      }
    }
    .appbar {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      min-height: 56px;
      padding: env(safe-area-inset-top) var(--space-4) 0;
      background: var(--surface);
      border-bottom: 1px solid var(--line);
    }
    .back {
      display: grid;
      place-items: center;
      width: 44px;
      height: 44px;
      margin-left: calc(-1 * var(--space-3));
      padding: 0;
      color: var(--ink);
      background: transparent;
      border: 0;
      border-radius: 50%;
      cursor: pointer;
    }
    .back:active {
      background: var(--accent-soft);
    }
    .back:focus-visible {
      outline: 2px solid var(--accent);
    }
    h1 {
      flex: 1;
      min-width: 0;
      margin: 0;
      font-size: var(--text-h3);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .demo {
      min-height: 32px;
      padding: 0 var(--space-2);
      font: inherit;
      font-size: var(--text-caption);
      color: var(--muted);
      background: transparent;
      border: 1px dashed var(--muted);
      border-radius: 2px;
      cursor: pointer;
    }
    .demo[aria-expanded='true'] {
      color: var(--ink);
      border-style: solid;
    }
    .demo-panel {
      display: flex;
      justify-content: flex-end;
      padding: var(--space-2) var(--space-4);
      background: var(--surface);
      border-bottom: 1px dashed var(--line);
    }
    .notice {
      margin: var(--space-2) var(--space-4) 0;
    }
    .screen {
      flex: 1;
      overflow-y: auto;
      overscroll-behavior: contain;
      padding: var(--space-4);
    }
    .tabs {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      padding-bottom: env(safe-area-inset-bottom);
      background: var(--surface);
      border-top: 1px solid var(--line);
    }
    .tabs a {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 2px;
      min-height: 56px;
      color: var(--muted);
      text-decoration: none;
      font-size: var(--text-caption);
      -webkit-tap-highlight-color: transparent;
    }
    .tabs a.active {
      color: var(--accent);
      font-weight: 600;
    }
    .tabs a:focus-visible {
      outline: 2px solid var(--accent);
      outline-offset: -4px;
    }
    svg {
      width: 24px;
      height: 24px;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.6;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
  `,
})
export class VendorShell {
  protected readonly store = inject(VendorStore);
  private readonly router = inject(Router);

  protected readonly tabs = TABS;
  protected readonly demoOpen = signal(false);
  private readonly scroller = viewChild.required<ElementRef<HTMLElement>>('scroller');
  private readonly location = inject(Location);

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((e) => {
      // Every screen is a fresh one: the last screen's notice stays behind (a submit sets its own after arriving)
      if (e instanceof NavigationStart) this.store.notice.set(null);
      // The screens share one scroll container, which the router's scroll restoration (window only) never touches
      if (e instanceof NavigationEnd) this.scroller().nativeElement.scrollTo({ top: 0 });
    });
  }

  /** The active screen's name for the app bar, and whether it is a sub-screen with ←, from the child route's data. */
  protected readonly screen = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      startWith(null),
      map(() => {
        let r = this.router.routerState.snapshot.root;
        while (r.firstChild) r = r.firstChild;
        return { heading: (r.data['heading'] as string | undefined) ?? '', back: r.data['back'] === true };
      }),
    ),
    { initialValue: { heading: '', back: false } },
  );

  /** Back where the vendor came from inside the vendor app; otherwise (a direct link, another area) to the requests tab. */
  protected back(): void {
    const previous = this.router.lastSuccessfulNavigation()?.previousNavigation;
    const url = previous && this.router.serializeUrl(previous.finalUrl ?? previous.extractedUrl);
    if (url?.startsWith('/vendor')) this.location.back();
    else void this.router.navigateByUrl('/vendor/requests');
  }
}

import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Tag } from 'primeng/tag';

/** Guardrail 5: every screen says the data is mock. */
@Component({
  selector: 'app-mock-badge',
  imports: [Tag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Its own look, not a status: a dashed "stamp" in ink, so "this data is mock" never reads as "something is wrong"
  template: `<p-tag role="note" value="ข้อมูลจำลอง" severity="secondary" />`,
  styles: `
    :host {
      white-space: nowrap;
    }
    p-tag {
      background: transparent;
      color: var(--ink);
      box-shadow: inset 0 0 0 1px var(--muted);
      border-radius: 2px;
      letter-spacing: 0.02em;
    }
  `,
})
export class MockBadge {}

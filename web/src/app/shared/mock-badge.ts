import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Guardrail 5: every screen says the data is mock. */
@Component({
  selector: 'app-mock-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" role="note">ข้อมูลจำลอง</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 8px;
      border: 1px solid var(--warn);
      border-radius: 4px;
      color: var(--warn);
      font-size: 0.75rem;
      font-weight: 600;
      white-space: nowrap;
    }
  `,
})
export class MockBadge {}

import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Tag } from 'primeng/tag';

/** Guardrail 5: every screen says the data is mock. */
@Component({
  selector: 'app-mock-badge',
  imports: [Tag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p-tag role="note" value="ข้อมูลจำลอง" severity="warn" />`,
  styles: `
    :host {
      white-space: nowrap;
    }
  `,
})
export class MockBadge {}

import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface RoundStep {
  label: string;
  state: 'done' | 'current' | 'todo';
}

/** Where a round stands: done steps in the accent, the current one marked and announced, the rest muted. */
@Component({
  selector: 'app-round-steps',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="steps" aria-label="ขั้นตอนของรอบ">
      @for (s of steps(); track s.label) {
        <li [attr.data-state]="s.state" [attr.aria-current]="s.state === 'current' ? 'step' : null">
          {{ s.label }}
          @if (s.state === 'done') {
            <span class="sr-only">(เสร็จแล้ว)</span>
          }
        </li>
      }
    </ol>
  `,
  styles: `
    .steps {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-1) 0;
      list-style: none;
      margin: var(--space-1) 0 var(--space-4);
      padding: 0;
      font-size: var(--text-small);
      color: var(--muted);
    }
    .steps li {
      display: flex;
      align-items: center;
      gap: var(--space-2);
    }
    .steps li + li::before {
      content: '';
      order: -2;
      width: 28px;
      height: 1px;
      margin: 0 var(--space-2);
      background: var(--line);
    }
    .steps li[data-state='done'] {
      color: var(--accent);
    }
    .steps li[data-state='done'] + li::before {
      background: var(--accent);
    }
    .steps li[data-state='current'] {
      color: var(--ink);
      font-weight: 600;
    }
    .steps li[data-state='done']::after,
    .steps li[data-state='current']::after {
      content: '';
      order: -1;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      border: 2px solid var(--accent);
    }
    .steps li[data-state='done']::after {
      background: var(--accent);
    }
  `,
})
export class RoundSteps {
  readonly steps = input.required<RoundStep[]>();
}

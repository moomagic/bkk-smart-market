import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface RoundStep {
  label: string;
  /** missing: the step happened (the round is past it) but nothing of it is on record */
  state: 'done' | 'current' | 'todo' | 'missing';
  note?: string;
}

/**
 * Where a round stands, read the way the vendor's request steps are: done is a filled marker, the current step a ring
 * with its label in the accent, the rest quiet. Each state is also named for assistive tech, so none rests on colour.
 */
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
          @if (s.note) {
            <span class="note">· {{ s.note }}</span>
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
      width: var(--space-6);
      height: 1px;
      margin: 0 var(--space-2);
      background: var(--line);
    }
    .steps li::after {
      content: '';
      order: -1;
      box-sizing: border-box;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      border: 1.5px solid var(--line);
      background: var(--surface);
    }
    .steps li[data-state='done'] {
      color: var(--ink);
    }
    .steps li[data-state='done']::after {
      border-color: var(--accent);
      background: var(--accent);
    }
    .steps li[data-state='done'] + li::before {
      background: var(--accent);
    }
    .steps li[data-state='missing']::after {
      border-style: dashed;
      border-color: var(--muted);
    }
    .steps li[data-state='missing'] + li::before {
      background: var(--accent);
    }
    .steps .note {
      color: var(--muted);
    }
    .steps li[data-state='current'] {
      color: var(--accent);
      font-weight: 600;
    }
    .steps li[data-state='current']::after {
      border: 2px solid var(--accent);
      background: var(--accent);
      box-shadow: inset 0 0 0 1.5px var(--surface);
    }
  `,
})
export class RoundSteps {
  readonly steps = input.required<RoundStep[]>();
}

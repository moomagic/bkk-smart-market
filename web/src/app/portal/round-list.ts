import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RoundSummary } from '../core/api';
import { StatusTag } from '../shared/status-tag';
import { formatThaiDate } from '../shared/labels';

/** The portal's round picker: one button per round (name, status, period, request count), the open one marked. */
@Component({
  selector: 'app-round-list',
  imports: [StatusTag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="rounds" [attr.aria-label]="label()">
      @for (round of rounds(); track round.id) {
        <li [class.selected]="round.id === selectedId()">
          <button type="button" class="round-pick" [attr.aria-current]="round.id === selectedId() ? 'true' : null"
            (click)="pick.emit(round)">
            <span class="round-line">
              <span class="round-name">{{ round.spaceName }}</span>
              <app-status-tag [round]="round.status" />
            </span>
            <span class="round-meta">
              {{ date(round.startsOn) }} – {{ date(round.endsOn) }} · คำขอ <span class="mono">{{ round.requestCount }}</span>
            </span>
          </button>
        </li>
      }
    </ul>
  `,
  styles: `
    .rounds {
      list-style: none;
      margin: 0;
      padding: 0;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--radius-lg);
    }
    .rounds > li + li {
      border-top: 1px solid var(--line);
    }
    .round-pick {
      display: grid;
      gap: var(--space-1);
      width: 100%;
      padding: var(--space-2) var(--space-3);
      border: 0;
      background: transparent;
      font: inherit;
      color: inherit;
      text-align: left;
      cursor: pointer;
    }
    .round-pick:hover {
      background: var(--ground);
    }
    .rounds > li.selected .round-pick {
      background: var(--accent-soft);
      box-shadow: inset 3px 0 0 var(--accent);
    }
    .round-line {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: var(--space-2);
    }
    .round-line app-status-tag {
      flex-shrink: 0;
      white-space: nowrap;
    }
    .round-name {
      font-weight: 600;
      color: var(--accent);
    }
    .round-meta {
      font-size: var(--text-small);
      color: var(--muted);
    }
  `,
})
export class RoundList {
  readonly rounds = input.required<RoundSummary[]>();
  readonly selectedId = input<string | null>(null);
  readonly label = input.required<string>();
  readonly pick = output<RoundSummary>();

  protected readonly date = formatThaiDate;
}

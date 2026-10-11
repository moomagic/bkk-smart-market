import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { SpaceSummary } from '../core/api';
import { SPACE_TYPE_LABEL, formatThaiDate } from '../shared/labels';

/**
 * A space's facts under its name, in the same two lines wherever a space is listed (matches and the full list):
 * what it is and how many locks, then when it trades and which round is open.
 */
@Component({
  selector: 'app-space-facts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let s = space();
    <p class="kind">{{ typeLabel[s.type] }} · <span class="mono">{{ s.lockCount }}</span> ล็อค · {{ s.productTypes.join(' · ') }}</p>
    @if (s.openRound; as round) {
      <p class="when">
        <span>{{ s.openHours }}</span> · <span>รอบ {{ date(round.startsOn) }} – {{ date(round.endsOn) }}</span>
        <!-- Every space shares one apply-by date most of the time: the page states it once, a row only when it differs -->
        @if (showCloses()) {
          · <span>ปิดรับ {{ date(round.applyClosesAt) }}</span>
        }
      </p>
    } @else {
      <p class="when"><span>{{ s.openHours }}</span> · <span>ไม่มีรอบที่เปิดรับคำขอ</span></p>
    }
  `,
  styles: `
    :host { display: block; }
    p { margin: var(--space-1) 0 0; }
    .when { color: var(--muted); font-size: var(--text-small); }
    /* Hours and round dates wrap as whole pieces, never leaving a year on a line of its own */
    .when span { white-space: nowrap; }
  `,
})
export class SpaceFacts {
  readonly space = input.required<SpaceSummary>();
  readonly showCloses = input(false);

  protected readonly typeLabel = SPACE_TYPE_LABEL;
  protected readonly date = formatThaiDate;
}

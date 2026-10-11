import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Tag } from 'primeng/tag';
import { AllocationRunStatus, OutcomeKind, RoundStatus, StallRequestStatus } from '../core/api';
import { OUTCOME_LABEL, ROUND_STATUS_LABEL, RUN_STATUS_LABEL, STATUS_LABEL } from './labels';

/**
 * Tag role (theme preset › tag): success = a good, standing result · info = in progress, nothing to do yet ·
 * secondary = over and done · warn = someone has to act. Warn stays rare so it still means something.
 */
type Severity = 'success' | 'info' | 'secondary' | 'warn';

const REQUEST_SEVERITY: Record<StallRequestStatus, Severity> = {
  Submitted: 'info',
  Eligible: 'info',
  Ineligible: 'warn',
  Won: 'success',
  // A waitlisted vendor can still move up, so it is in progress rather than over
  Waitlisted: 'info',
  NotSelected: 'secondary',
  Confirmed: 'success',
  Forfeited: 'secondary',
};

const ROUND_SEVERITY: Record<RoundStatus, Severity> = {
  Open: 'success',
  Closed: 'info',
  // Drawn waits on the officer's approve or reject
  Drawn: 'warn',
  // Nothing left to decide in an approved round; the list's attention goes to the rounds still in play
  Approved: 'secondary',
};

const RUN_SEVERITY: Record<AllocationRunStatus, Severity> = {
  Draft: 'warn',
  Approved: 'success',
  Rejected: 'secondary',
};

const OUTCOME_SEVERITY: Record<OutcomeKind, Severity> = {
  Won: 'success',
  Waitlisted: 'info',
  NotSelected: 'secondary',
  Forfeited: 'secondary',
};

/**
 * A request, round, allocation-run or draw-outcome status as a tag. The text always comes from the shared Thai label maps,
 * so a submitted request reads "ยื่นคำขอแล้ว" and never anything that sounds like a booking (Guardrail 4).
 * Pass exactly one of the inputs.
 */
@Component({
  selector: 'app-status-tag',
  imports: [Tag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (view(); as v) {
      <p-tag [value]="v.label" [severity]="v.severity" />
    }
  `,
})
export class StatusTag {
  readonly request = input<StallRequestStatus>();
  readonly round = input<RoundStatus>();
  readonly run = input<AllocationRunStatus>();
  readonly outcome = input<OutcomeKind>();

  protected readonly view = computed(() => {
    const request = this.request();
    if (request) return { label: STATUS_LABEL[request], severity: REQUEST_SEVERITY[request] };
    const round = this.round();
    if (round) return { label: ROUND_STATUS_LABEL[round], severity: ROUND_SEVERITY[round] };
    const run = this.run();
    if (run) return { label: RUN_STATUS_LABEL[run], severity: RUN_SEVERITY[run] };
    const outcome = this.outcome();
    if (outcome) return { label: OUTCOME_LABEL[outcome], severity: OUTCOME_SEVERITY[outcome] };
    return null;
  });
}

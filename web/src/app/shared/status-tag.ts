import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Tag } from 'primeng/tag';
import { AllocationRunStatus, RoundStatus, StallRequestStatus } from '../core/api';
import { ROUND_STATUS_LABEL, RUN_STATUS_LABEL, STATUS_LABEL } from './labels';

/** Tag colour: undefined = primary (accent) for good news, secondary = neutral, warn = needs attention. */
type Severity = 'secondary' | 'warn' | undefined;

const REQUEST_SEVERITY: Record<StallRequestStatus, Severity> = {
  Submitted: 'secondary',
  Eligible: undefined,
  Ineligible: 'warn',
  Won: undefined,
  Waitlisted: 'secondary',
  NotSelected: 'secondary',
  Confirmed: undefined,
  Forfeited: 'secondary',
};

const ROUND_SEVERITY: Record<RoundStatus, Severity> = {
  Open: undefined,
  Closed: 'secondary',
  Drawn: 'warn',
  Approved: undefined,
};

const RUN_SEVERITY: Record<AllocationRunStatus, Severity> = {
  Draft: 'warn',
  Approved: undefined,
  Rejected: 'secondary',
};

/**
 * A request, round or allocation-run status as a tag. The text always comes from the shared Thai label maps,
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

  protected readonly view = computed(() => {
    const request = this.request();
    if (request) return { label: STATUS_LABEL[request], severity: REQUEST_SEVERITY[request] };
    const round = this.round();
    if (round) return { label: ROUND_STATUS_LABEL[round], severity: ROUND_SEVERITY[round] };
    const run = this.run();
    if (run) return { label: RUN_STATUS_LABEL[run], severity: RUN_SEVERITY[run] };
    return null;
  });
}

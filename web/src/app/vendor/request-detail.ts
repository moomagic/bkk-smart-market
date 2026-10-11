import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Checkbox } from 'primeng/checkbox';
import { Textarea } from 'primeng/textarea';
import { StallRequest } from '../core/api';
import { StatusTag } from '../shared/status-tag';
import { CATEGORY_LABEL, REVIEW_OUTCOME_LABEL, SPACE_TYPE_LABEL, formatThaiDate, formatThaiDateTime } from '../shared/labels';
import { VENDOR_RUN_STATUS, canReview, nextStep, pendingSteps } from './request-text';
import { VendorStore } from './vendor-store';

/** One request on its own screen: the result and its reasons, and every action on it, the main one kept at the bottom. */
@Component({
  selector: 'app-request-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, Button, Checkbox, Textarea, StatusTag],
  templateUrl: './request-detail.html',
  styleUrl: './request-detail.scss',
})
export class RequestDetail {
  private readonly store = inject(VendorStore);

  /** From the route, `/vendor/requests/:id`. */
  readonly id = input.required<string>();

  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly spaceType = SPACE_TYPE_LABEL;
  protected readonly stepState = { done: 'เสร็จแล้ว', current: 'ขั้นตอนปัจจุบัน', upcoming: 'ขั้นต่อไป' } as const;
  protected readonly reviewOutcome = REVIEW_OUTCOME_LABEL;
  protected readonly runStatus = VENDOR_RUN_STATUS;
  protected readonly date = formatThaiDate;
  protected readonly dateTime = formatThaiDateTime;
  protected readonly next = nextStep;
  protected readonly reviewable = canReview;

  protected readonly mine = this.store.myRequests;
  protected readonly acting = this.store.acting;
  protected readonly outsideGeofence = this.store.outsideGeofence;

  protected readonly request = computed(() => {
    const mine = this.mine();
    return mine?.state === 'ready' ? (mine.data.find((r) => r.id === this.id()) ?? null) : null;
  });

  /** The listed space the request is for: its type, locks and hours. Null until the list loads. */
  protected readonly space = computed(() => {
    const req = this.request();
    return (req && this.store.spaceList().find((s) => s.id === req.spaceId)) ?? null;
  });

  /** The apply-by date, while the request's round is the one still open at its space. */
  protected readonly closesAt = computed(() => {
    const round = this.space()?.openRound;
    return round && round.id === this.request()?.roundId ? round.applyClosesAt : null;
  });

  /** The steps of a request still waiting on the district (submitted or eligible); null in every other state. */
  protected readonly steps = computed(() => {
    const req = this.request();
    return req ? pendingSteps(req) : null;
  });

  /** The forfeit waits for a second, deliberate click. */
  protected readonly confirmingForfeit = signal(false);
  protected readonly reviewing = signal(false);
  protected readonly reviewReason = signal('');

  constructor() {
    // The component stays when only :id changes; a prompt or a half-typed reason belongs to the request it was for
    effect(() => {
      this.id();
      untracked(() => {
        this.confirmingForfeit.set(false);
        this.reviewing.set(false);
        this.reviewReason.set('');
      });
    });
  }

  protected reload(): void {
    this.store.reload();
  }

  protected async confirm(req: StallRequest): Promise<void> {
    await this.store.confirm(req);
    this.confirmingForfeit.set(false);
  }

  protected async forfeit(req: StallRequest): Promise<void> {
    await this.store.forfeit(req);
    this.confirmingForfeit.set(false);
  }

  /** Simulates scanning the QR posted at the vendor's own lock. */
  protected checkIn(req: StallRequest): void {
    this.store.checkIn(req);
  }

  protected async fileReview(req: StallRequest): Promise<void> {
    const reason = this.reviewReason().trim();
    if (!reason) return;
    if (await this.store.fileReview(req, reason)) {
      this.reviewing.set(false);
      this.reviewReason.set('');
    }
  }
}

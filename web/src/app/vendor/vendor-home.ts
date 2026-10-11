import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { Card } from 'primeng/card';
import { Checkbox } from 'primeng/checkbox';
import { Message } from 'primeng/message';
import { Textarea } from 'primeng/textarea';
import { AllocationRunStatus, SpaceSummary, StallRequest, StallRequestStatus } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { AssistantChat } from './assistant-chat';
import { SpaceMatching } from './space-matching';
import { VendorStore } from './vendor-store';
import { StatusTag } from '../shared/status-tag';
import {
  CATEGORY_LABEL,
  REVIEW_OUTCOME_LABEL,
  RUN_STATUS_LABEL,
  SPACE_TYPE_LABEL,
  formatThaiDate,
  formatThaiDateTime,
} from '../shared/labels';

const IN_PLAY = new Set<StallRequestStatus>(['Submitted', 'Eligible', 'Ineligible', 'Won', 'Waitlisted']);

@Component({
  selector: 'app-vendor-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgTemplateOutlet, Button, Card, Checkbox, Message, Textarea, AssistantChat, SpaceMatching, StatusTag],
  providers: [VendorStore],
  templateUrl: './vendor-home.html',
  styleUrl: './vendor-home.scss',
})
export class VendorHome {
  private readonly store = inject(VendorStore);
  protected readonly session = inject(DemoSession);

  protected readonly typeLabel = SPACE_TYPE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly date = formatThaiDate;
  protected readonly dateTime = formatThaiDateTime;
  protected readonly runStatus = RUN_STATUS_LABEL;
  protected readonly reviewOutcome = REVIEW_OUTCOME_LABEL;
  /** Request id whose review form is open, and the reason typed so far. */
  protected readonly reviewing = signal<string | null>(null);
  protected readonly reviewReason = signal('');
  /** Request id whose forfeit is waiting for a second, deliberate click. */
  protected readonly confirmingForfeit = signal<string | null>(null);

  protected readonly spaces = this.store.spaces;
  protected readonly spaceList = this.store.spaceList;
  protected readonly myRequests = this.store.myRequests;
  protected readonly current = this.store.current;
  protected readonly finished = this.store.finished;
  protected readonly requestStatusByRound = this.store.requestStatusByRound;
  protected readonly submittable = this.store.submittable;
  protected readonly requestStatus = this.store.requestStatus;
  protected readonly closesAt = this.store.closesAt;
  protected readonly submitting = this.store.submitting;
  protected readonly acting = this.store.acting;
  protected readonly outsideGeofence = this.store.outsideGeofence;
  protected readonly notice = this.store.notice;

  /**
   * The assistant starts open until the vendor has an application in play (submitted, being checked, won or
   * waitlisted, or in a round open now); decided once, on first load, so it never folds away under the vendor's
   * hand after a submit. A lock already confirmed from an earlier round does not count: that vendor may be
   * asking about the next one.
   */
  protected readonly assistantOpen = signal(true);
  /** Space Matching folds away on the same rule: once an application is in play it is not the next step. */
  protected readonly matchOpen = signal(true);

  constructor() {
    let decided = false;
    effect(() => {
      const mine = this.myRequests();
      if (decided || mine?.state !== 'ready' || this.spaces().state !== 'ready') return;
      decided = true;
      const open = new Set(this.spaceList().flatMap((s) => (s.openRound ? [s.openRound.id] : [])));
      const startOpen = !mine.data.some((r) => IN_PLAY.has(r.status) || open.has(r.roundId));
      this.assistantOpen.set(startOpen);
      this.matchOpen.set(startOpen);
    });
  }

  protected submit(space: SpaceSummary): void {
    void this.store.submit(space);
  }

  /** The vendor-facing word for a run: they wait on the district, they do not hold a draft. */
  protected readonly vendorRunStatus: Record<AllocationRunStatus, string> = {
    Draft: 'รอเขตอนุมัติ',
    Approved: 'ประกาศผลแล้ว',
    Rejected: 'เขตไม่อนุมัติ จับฉลากใหม่',
  };

  /** One plain line on what happens next, for the states where the vendor is waiting rather than deciding. */
  protected nextStep(req: StallRequest): string | null {
    switch (req.status) {
      case 'Submitted':
        return 'รอเขตตรวจสิทธิ';
      case 'Eligible':
        return req.runs.some((r) => r.status === 'Draft')
          ? 'เขตจับฉลากแล้ว รออนุมัติผล'
          : 'ผ่านการตรวจสิทธิแล้ว รอเขตจับฉลาก';
      case 'Ineligible':
        return this.canReview(req) ? 'ถ้าคิดว่าตรวจสิทธิผิด ขอทบทวนผลได้ด้านล่าง' : null;
      case 'Waitlisted':
        return req.waitlistPosition
          ? `สำรองลำดับ ${req.waitlistPosition} · ถ้ามีผู้ได้สิทธิ์สละ คุณจะเลื่อนขึ้นตามลำดับ`
          : 'สำรอง · ถ้ามีผู้ได้สิทธิ์สละ คุณจะเลื่อนขึ้นตามลำดับ';
      case 'NotSelected':
        return 'ถ้ายื่นรอบถัดไปของพื้นที่นี้ จะได้น้ำหนัก ×2 ในการจับฉลาก';
      default:
        return null;
    }
  }

  /** The request-card template hands its row over untyped; this restores the type for the checker. */
  protected asRequest(req: StallRequest): StallRequest {
    return req;
  }

  /** Retry after a failed load. */
  protected reload(): void {
    this.store.reload();
  }

  protected async confirm(req: StallRequest): Promise<void> {
    await this.store.confirm(req);
    this.confirmingForfeit.set(null);
  }

  protected async forfeit(req: StallRequest): Promise<void> {
    await this.store.forfeit(req);
    this.confirmingForfeit.set(null);
  }

  /** Simulates scanning the QR posted at the vendor's own lock. */
  protected checkIn(req: StallRequest): void {
    this.store.checkIn(req);
  }

  /** Only an Ineligible result can be reviewed (draw outcomes cannot), once per request; the server checks the window. */
  protected canReview(req: StallRequest): boolean {
    return req.status === 'Ineligible' && !req.review;
  }

  protected async fileReview(req: StallRequest): Promise<void> {
    const reason = this.reviewReason().trim();
    if (!reason) return;
    if (await this.store.fileReview(req, reason)) {
      this.reviewing.set(null);
      this.reviewReason.set('');
    }
  }
}

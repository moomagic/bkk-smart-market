import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { Card } from 'primeng/card';
import { Checkbox } from 'primeng/checkbox';
import { Textarea } from 'primeng/textarea';
import { AllocationRunStatus, StallRequest } from '../core/api';
import { VendorStore } from './vendor-store';
import { StatusTag } from '../shared/status-tag';
import {
  CATEGORY_LABEL,
  REVIEW_OUTCOME_LABEL,
  RUN_STATUS_LABEL,
  formatThaiDate,
  formatThaiDateTime,
} from '../shared/labels';

@Component({
  selector: 'app-vendor-requests',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgTemplateOutlet, Button, Card, Checkbox, Textarea, StatusTag],
  templateUrl: './vendor-requests.html',
  styleUrl: './vendor-requests.scss',
})
/** Tab คำขอ: the vendor's requests, what happens next, and the actions on each. */
export class VendorRequests {
  private readonly store = inject(VendorStore);

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

  protected readonly myRequests = this.store.myRequests;
  protected readonly current = this.store.current;
  protected readonly finished = this.store.finished;
  protected readonly acting = this.store.acting;
  protected readonly outsideGeofence = this.store.outsideGeofence;

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

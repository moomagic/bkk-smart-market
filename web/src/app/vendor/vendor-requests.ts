import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Button, ButtonDirective } from 'primeng/button';
import { StallRequest } from '../core/api';
import { StatusTag } from '../shared/status-tag';
import { CATEGORY_LABEL, formatThaiDate, formatThaiDateTime } from '../shared/labels';
import { DEMO_ROLES, DemoSession } from '../core/demo-session';
import { canReview, cardLine, nextStep } from './request-text';
import { VendorStore } from './vendor-store';

/** The one line under the greeting: what the vendor has to do now, and where to do it. */
type Todo = { text: string; urgent?: boolean; requestId?: string; apply?: boolean };

const WAITING = new Set(['Submitted', 'Eligible', 'Waitlisted']);

/** Tab คำขอ: a greeting with the one thing to do now, then the vendor's requests, each leading to its own screen. */
@Component({
  selector: 'app-vendor-requests',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, RouterLink, Button, ButtonDirective, StatusTag],
  templateUrl: './vendor-requests.html',
  styleUrl: './vendor-requests.scss',
})
export class VendorRequests {
  private readonly store = inject(VendorStore);

  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly date = formatThaiDate;
  protected readonly line = cardLine;

  private readonly session = inject(DemoSession);
  /** The short form only (Guardrail 5); other roles are not greeted. */
  protected readonly name = computed(() => DEMO_ROLES[this.session.role()].name ?? null);

  /**
   * In order: a won lock to confirm (it has a deadline), an eligibility result the vendor may contest, a request
   * still waiting on the district, an open round not yet applied to. Nothing pending says nothing.
   */
  protected readonly todo = computed((): Todo | null => {
    const won = this.store.awaitingConfirm()[0];
    if (won) {
      return {
        text: `ยืนยันสิทธิ์ล็อค ${won.lockCode} ภายใน ${formatThaiDateTime(won.confirmBy!)}`,
        urgent: true,
        requestId: won.id,
      };
    }
    const current = this.store.current();
    const contest = current.find(canReview);
    if (contest) return { text: `${contest.spaceName} ไม่ผ่านการตรวจสิทธิ ขอทบทวนผลได้`, requestId: contest.id };
    const waiting = current.find((r) => WAITING.has(r.status));
    if (waiting) return { text: `${waiting.spaceName} · ${nextStep(waiting)}`, requestId: waiting.id };
    if (this.store.canApplyNow()) {
      const close = this.store.closesAt();
      return { text: close ? `รับคำขอรอบใหม่ถึง ${formatThaiDate(close)}` : 'เปิดรับคำขอรอบใหม่อยู่', apply: true };
    }
    return null;
  });

  protected readonly myRequests = this.store.myRequests;
  protected readonly current = this.store.current;
  protected readonly finished = this.store.finished;

  /** The request-card template hands its row over untyped; this restores the type for the checker. */
  protected asRequest(req: StallRequest): StallRequest {
    return req;
  }

  /** Retry after a failed load. */
  protected reload(): void {
    this.store.reload();
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { AllocationRun, Api, ReviewOutcome, RoundRequestRow, RoundReview, RoundSummary, problemMessage } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { AuditTimeline } from './audit-timeline';
import {
  CATEGORY_LABEL,
  OUTCOME_LABEL,
  REVIEW_OUTCOME_LABEL,
  ROUND_STATUS_LABEL,
  RUN_STATUS_LABEL,
  STATUS_LABEL,
  formatThaiDate,
  formatThaiDateTime,
} from '../shared/labels';

type Load<T> = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: T };

@Component({
  selector: 'app-portal-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AuditTimeline],
  templateUrl: './portal-home.html',
  styleUrl: './portal-home.scss',
})
export class PortalHome {
  private readonly api = inject(Api);
  protected readonly session = inject(DemoSession);

  protected readonly date = formatThaiDate;
  protected readonly roundStatus = ROUND_STATUS_LABEL;
  protected readonly status = STATUS_LABEL;
  protected readonly category = CATEGORY_LABEL;
  protected readonly dateTime = formatThaiDateTime;
  protected readonly runStatus = RUN_STATUS_LABEL;
  protected readonly outcome = OUTCOME_LABEL;
  protected readonly reviewOutcome = REVIEW_OUTCOME_LABEL;

  protected readonly isOfficer = computed(() => this.session.role() !== 'vendor');
  /** Past the round's reject limit only an executive may reject (ADR-0002). */
  protected readonly isExecutive = computed(() => this.session.role() === 'executive');
  private readonly refresh = signal(0);
  protected readonly selectedId = signal<string | null>(null);

  protected readonly rounds = toSignal(
    combineLatest([toObservable(this.isOfficer), toObservable(this.refresh)]).pipe(
      switchMap(([officer]) => (officer ? load(this.api.rounds()) : of({ state: 'ready', data: [] } as Load<RoundSummary[]>))),
    ),
    { initialValue: { state: 'loading' } as Load<RoundSummary[]> },
  );

  protected readonly roundList = computed(() => {
    const r = this.rounds();
    return r.state === 'ready' ? r.data : [];
  });

  protected readonly selected = computed(() => this.roundList().find((r) => r.id === this.selectedId()) ?? null);

  protected readonly requests = toSignal(
    combineLatest([toObservable(this.selectedId), toObservable(this.refresh)]).pipe(
      switchMap(([id]) => (id ? load(this.api.roundRequests(id)) : of(null))),
    ),
    { initialValue: null as Load<RoundRequestRow[]> | null },
  );

  protected readonly requestRows = computed(() => {
    const r = this.requests();
    return r?.state === 'ready' ? r.data : [];
  });

  /** Every run of the selected round, newest first (rejected ones stay visible). */
  protected readonly runs = toSignal(
    combineLatest([toObservable(this.selectedId), toObservable(this.refresh)]).pipe(
      switchMap(([id]) => (id ? load(this.api.allocationRuns(id)) : of(null))),
    ),
    { initialValue: null as Load<AllocationRun[]> | null },
  );

  protected readonly runList = computed(() => {
    const r = this.runs();
    return r?.state === 'ready' ? r.data : [];
  });

  protected readonly reviews = toSignal(
    combineLatest([toObservable(this.selectedId), toObservable(this.refresh)]).pipe(
      switchMap(([id]) => (id ? load(this.api.roundReviews(id)) : of(null))),
    ),
    { initialValue: null as Load<RoundReview[]> | null },
  );

  protected readonly reviewList = computed(() => {
    const r = this.reviews();
    return r?.state === 'ready' ? r.data : [];
  });

  /** Review being answered, with the chosen outcome and the answer typed so far. */
  protected readonly answering = signal<{ id: string; outcome: ReviewOutcome; response: string } | null>(null);

  protected readonly latestRun = computed(() => this.runList()[0] ?? null);

  protected readonly uncheckedCount = computed(() => this.requestRows().filter((r) => r.status === 'Submitted').length);

  /** Mirrors the API guards so the button only offers what the server will accept; the server still decides. */
  protected readonly canRun = computed(() => {
    const round = this.selected();
    const runs = this.runs();
    return (
      !!round &&
      (round.status === 'Open' || round.status === 'Closed') &&
      runs?.state === 'ready' &&
      !runs.data.some((r) => r.status !== 'Rejected') &&
      this.requests()?.state === 'ready' &&
      this.uncheckedCount() === 0 &&
      this.requestRows().some((r) => r.status === 'Eligible')
    );
  });

  /** Demo clock (Development only); null when the API does not offer it. */
  protected readonly clock = toSignal(
    combineLatest([toObservable(this.isOfficer), toObservable(this.refresh)]).pipe(
      switchMap(([officer]) =>
        officer ? this.api.demoClock().pipe(map((c) => c.now), catchError(() => of(null))) : of(null),
      ),
    ),
    { initialValue: null as string | null },
  );

  /** What the audit panel shows: the selected round, or one request in it. */
  protected readonly auditTarget = signal<{ type: string; id: string; label: string } | null>(null);
  protected readonly refreshCount = this.refresh.asReadonly();

  protected readonly running = signal(false);
  protected readonly deciding = signal(false);
  /** Run id whose reject form is open, and the reason typed so far. */
  protected readonly rejecting = signal<string | null>(null);
  protected readonly rejectReason = signal('');
  protected readonly checking = signal(false);
  protected readonly notice = signal<{ kind: 'ok' | 'error'; text: string } | null>(null);

  protected select(round: RoundSummary): void {
    this.notice.set(null);
    this.auditTarget.set(null);
    this.selectedId.set(round.id);
  }

  protected checkEligibility(round: RoundSummary): void {
    this.checking.set(true);
    this.notice.set(null);
    this.api.checkEligibility(round.id).subscribe({
      next: (rows) => {
        this.checking.set(false);
        const failed = rows.filter((r) => r.status === 'Ineligible').length;
        this.notice.set({ kind: 'ok', text: `ตรวจสิทธิแล้ว ${rows.length} คำขอ · ไม่ผ่าน ${failed}` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.checking.set(false);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  protected runAllocation(round: RoundSummary): void {
    this.running.set(true);
    this.notice.set(null);
    this.api.runAllocation(round.id).subscribe({
      next: (run) => {
        this.running.set(false);
        this.notice.set({ kind: 'ok', text: `รันจัดสรรแล้ว · ${run.referenceNo} · ร่างรออนุมัติ` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.running.set(false);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  protected reject(run: AllocationRun): void {
    const reason = this.rejectReason().trim();
    if (!reason) return;
    this.deciding.set(true);
    this.notice.set(null);
    this.api.rejectAllocationRun(run.id, reason).subscribe({
      next: (r) => {
        this.deciding.set(false);
        this.rejecting.set(null);
        this.rejectReason.set('');
        this.notice.set({ kind: 'ok', text: `ปฏิเสธผล ${r.referenceNo} แล้ว · ตรวจสิทธิใหม่ได้แล้วรันจัดสรรอีกครั้ง (seed ใหม่)` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.deciding.set(false);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  protected answer(review: RoundReview): void {
    const a = this.answering();
    if (!a || a.id !== review.id || !a.response.trim()) return;
    this.deciding.set(true);
    this.notice.set(null);
    this.api.respondReview(review.id, a.outcome, a.response.trim()).subscribe({
      next: (r) => {
        this.deciding.set(false);
        this.answering.set(null);
        const approved = this.selected()?.status === 'Approved';
        const hadDraft = this.runList().some((run) => run.status === 'Draft');
        const after = a.outcome !== 'Corrected' ? ''
          : approved ? ' · ได้ล็อคที่ว่างอยู่ หรือเข้าท้ายสำรองของกลุ่ม — ผู้ได้สิทธิ์เดิมไม่เปลี่ยน'
          : hadDraft ? ' · ผ่านการตรวจสิทธิ และร่างผลจัดสรรถูกปฏิเสธโดยไม่นับลิมิต — รันจัดสรรใหม่ได้'
          : ' · ผ่านการตรวจสิทธิแล้ว';
        this.notice.set({ kind: 'ok', text: `ตอบคำขอทบทวนของ ${review.vendorName}: ${REVIEW_OUTCOME_LABEL[r.outcome!]}${after}` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.deciding.set(false);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  protected approve(run: AllocationRun): void {
    this.deciding.set(true);
    this.notice.set(null);
    this.api.approveAllocationRun(run.id).subscribe({
      next: (approved) => {
        this.deciding.set(false);
        this.notice.set({ kind: 'ok', text: `อนุมัติผลจัดสรร ${approved.referenceNo} แล้ว · ผู้ค้าเห็นผลและต้องยืนยันสิทธิ์ภายในกำหนด` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.deciding.set(false);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  /** Two clicks: the first arms it, the second wipes everything back to the seed. */
  protected readonly confirmReset = signal(false);

  protected resetDemo(): void {
    if (!this.confirmReset()) {
      this.confirmReset.set(true);
      return;
    }
    this.confirmReset.set(false);
    this.api.resetDemo().subscribe({
      next: (c) => {
        this.selectedId.set(null);
        this.auditTarget.set(null);
        this.notice.set({ kind: 'ok', text: `เริ่ม demo ใหม่แล้ว · ข้อมูลกลับเป็นค่าเริ่มต้น · เวลา ${formatThaiDateTime(c.now)}` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => this.notice.set({ kind: 'error', text: problemMessage(err) }),
    });
  }

  protected advanceClock(days: number): void {
    const now = this.clock();
    if (!now) return;
    const next = new Date(new Date(now).getTime() + days * 86_400_000).toISOString();
    this.api.setDemoClock(next).subscribe({
      next: (c) => {
        this.notice.set({ kind: 'ok', text: `เลื่อนเวลา demo เป็น ${formatThaiDateTime(c.now)}` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => this.notice.set({ kind: 'error', text: problemMessage(err) }),
    });
  }

  protected showRoundAudit(round: RoundSummary): void {
    this.auditTarget.set({ type: 'Round', id: round.id, label: `รอบ ${round.spaceName}` });
  }

  protected showRequestAudit(row: RoundRequestRow): void {
    this.auditTarget.set({ type: 'StallRequest', id: row.requestId, label: `คำขอของ ${row.vendorName}` });
  }
}

function load<T>(source: Observable<T>) {
  return source.pipe(
    map((data) => ({ state: 'ready', data }) as Load<T>),
    catchError(() => of({ state: 'error' } as Load<T>)),
    startWith({ state: 'loading' } as Load<T>),
  );
}

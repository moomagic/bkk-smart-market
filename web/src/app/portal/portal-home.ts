import { ChangeDetectionStrategy, Component, Injector, afterNextRender, afterRenderEffect, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Badge } from 'primeng/badge';
import { Drawer } from 'primeng/drawer';
import { Popover } from 'primeng/popover';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { Button } from 'primeng/button';
import { RadioButton } from 'primeng/radiobutton';
import { Tag } from 'primeng/tag';
import { Textarea } from 'primeng/textarea';
import { Message } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { Observable, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { AllocationRun, Api, Category, InsightFlag, OutcomeKind, QuotaLine, ReviewOutcome, RoundRequestRow, RoundReview, RoundSummary, RunOutcome, problemMessage } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { ApproveRunDialog } from './approve-run-dialog';
import { RoundList } from './round-list';
import { RoundStep, RoundSteps } from './round-steps';
import { AuditTimeline } from './audit-timeline';
import { AuditNames } from './audit-view';
import { StatusTag } from '../shared/status-tag';
import {
  CATEGORY_LABEL,
  REVIEW_OUTCOME_LABEL,
  RUN_STATUS_LABEL,
  STATUS_LABEL,
  formatThaiDate,
  formatThaiDateTime,
} from '../shared/labels';

type RoundTab = 'requests' | 'run' | 'reviews' | 'insight';

const CATEGORY_ORDER: Category[] = ['Legacy', 'Vulnerable', 'New'];

type Load<T> = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: T };

/** A row of the outcome table: a vendor's outcome, or the one line of a quota group nobody applied in. */
type OutcomeRow = RunOutcome & { group: string; empty?: true };

@Component({
  selector: 'app-portal-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    Badge,
    Button,
    Drawer,
    Message,
    Popover,
    RadioButton,
    Tab,
    TabList,
    TabPanel,
    TabPanels,
    Tabs,
    TableModule,
    Tag,
    Textarea,
    ApproveRunDialog,
    RoundList,
    RoundSteps,
    AuditTimeline,
    StatusTag,
  ],
  templateUrl: './portal-home.html',
  styleUrl: './portal-home.scss',
})
export class PortalHome {
  private readonly api = inject(Api);
  protected readonly session = inject(DemoSession);

  protected readonly date = formatThaiDate;
  protected readonly status = STATUS_LABEL;
  protected readonly category = CATEGORY_LABEL;
  protected readonly dateTime = formatThaiDateTime;
  protected readonly runStatus = RUN_STATUS_LABEL;

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

  /**
   * Rounds still waiting on the district, the most pressing first: a draw to decide, then requests to check, then the
   * rest by start date. Approved ones (nothing left to decide) follow, newest first.
   */
  protected readonly currentRounds = computed(() =>
    this.roundList()
      .filter((r) => r.status !== 'Approved')
      .map((r, i) => ({ r, i, rank: urgency(r) }))
      .sort((a, b) => a.rank - b.rank || a.i - b.i)
      .map(({ r }) => r),
  );
  protected readonly pastRounds = computed(() =>
    this.roundList()
      .filter((r) => r.status === 'Approved')
      .sort((a, b) => b.startsOn.localeCompare(a.startsOn)),
  );

  /** Where the selected round stands, from its status and the eligibility checks done so far. */
  protected readonly steps = computed(() => {
    const round = this.selected();
    if (!round) return [];
    const current =
      round.status === 'Approved' ? 4
        : round.status === 'Drawn' ? 3
          : this.requestRows().length === 0 ? 0
            : this.uncheckedCount() > 0 ? 1
              : 2;
    return ['ยื่นคำขอ', 'ตรวจสิทธิ', 'จับฉลาก', 'อนุมัติผล'].map(
      (label, i): RoundStep => ({ label, state: i < current || current === 4 ? 'done' : i === current ? 'current' : 'todo' }),
    );
  });

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

  /** Masked names of the selected round's vendors, so the audit trail names people instead of ids. */
  protected readonly auditNames = computed<AuditNames>(() => {
    const rows = this.requestRows();
    return {
      requests: new Map(rows.map((r) => [r.requestId, r.vendorName])),
      vendors: new Map(rows.map((r) => [r.vendorId, r.vendorName])),
    };
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

  /** Allocation Insight flags of the selected round (read-only). */
  protected readonly insights = toSignal(
    combineLatest([toObservable(this.selectedId), toObservable(this.refresh)]).pipe(
      switchMap(([id]) => (id ? load(this.api.roundInsights(id)) : of(null))),
    ),
    { initialValue: null as Load<InsightFlag[]> | null },
  );

  protected readonly insightList = computed(() => {
    const r = this.insights();
    return r?.state === 'ready' ? r.data : [];
  });

  /** Some flag is more than a note, so the tab's count asks to be looked at. */
  protected readonly insightWarns = computed(() => this.insightList().some((f) => f.severity !== 'Info'));

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

  protected readonly latestRun = computed<AllocationRun | null>(() => this.runList()[0] ?? null);

  protected readonly pendingReviews = computed(() => this.reviewList().filter((r) => !r.outcome).length);

  /**
   * The latest run's outcomes in quota-group order. p-table sorts subheader groups by their group field, so the field
   * is a key that sorts in that order ("1-Legacy", "2-Vulnerable", "3-New"); rows keep the draw order inside a group.
   * Every quota group gets its header, as in the approve dialog: a group nobody applied in carries one empty row, so
   * the quota it handed on is shown where it left.
   */
  protected readonly outcomeRows = computed<OutcomeRow[]>(() => {
    const run = this.latestRun();
    if (!run) return [];
    const group = (c: Category) => `${CATEGORY_ORDER.indexOf(c) + 1}-${c}`;
    const rows: OutcomeRow[] = run.outcomes.map((o) => ({ ...o, group: group(o.category) }));
    if (!run.pooledDraw) {
      for (const q of run.quota) {
        if (q.base > 0 && !run.outcomes.some((o) => o.category === q.category)) {
          rows.push({
            vendorId: `empty-${q.category}`, vendorName: '', category: q.category, weight: 1, kind: 'NotSelected',
            lockCode: null, waitlistPosition: null, viaRollover: false, reason: '', group: group(q.category), empty: true,
          });
        }
      }
    }
    return rows.sort((a, b) => a.group.localeCompare(b.group));
  });

  /** The tab the officer picked for this round; null follows what the round needs next. */
  private readonly tabChoice = signal<RoundTab | null>(null);

  protected readonly tab = computed<RoundTab>(() => {
    const choice = this.tabChoice();
    const available = (t: RoundTab) =>
      t === 'requests' || (t === 'run' && this.hasRun()) || (t === 'reviews' && this.reviewList().length > 0) ||
      (t === 'insight' && this.insightList().length > 0);
    if (choice && available(choice)) return choice;
    // Unanswered reviews have a deadline; a drawn round waits for a decision; otherwise the requests
    if (this.pendingReviews() > 0) return 'reviews';
    const status = this.selected()?.status;
    // Decided from the round's status, not from the run arriving: the tab is right on first paint instead of
    // jumping across once the runs load
    if (status === 'Drawn' || status === 'Approved') return 'run';
    return 'requests';
  });

  private readonly tabList = viewChild(TabList);

  constructor() {
    // On arrival, open the round that needs the officer now: a draw waiting for a decision, else unchecked requests
    let picked = false;
    effect(() => {
      const list = this.roundList();
      if (picked || this.rounds().state !== 'ready' || untracked(this.selectedId)) return;
      picked = true;
      const next =
        list.find((r) => r.status === 'Drawn') ??
        list.find((r) => (r.status === 'Open' || r.status === 'Closed') && r.requestCount > r.eligibleCount + r.ineligibleCount);
      if (next) untracked(() => this.selectedId.set(next.id));
    });
    // PrimeNG places the active-tab bar when the selected tab changes, not when a tab's width does. The counts on
    // the tabs arrive after the round opens and widen them, so re-place the bar once they have rendered.
    afterRenderEffect(() => {
      this.requestRows().length;
      this.reviewList().length;
      this.pendingReviews();
      this.insightList().length;
      this.tab();
      this.tabList()?.updateInkBar();
    });
  }

  /** The round has a run to show, or will once the runs load (a drawn or approved round always has one). */
  protected readonly hasRun = computed(() => {
    const status = this.selected()?.status;
    return !!this.latestRun() || status === 'Drawn' || status === 'Approved';
  });

  private readonly injector = inject(Injector);

  /** "ตรวจผลและอนุมัติ": open the run tab and move focus to the decision bar, which stays on screen beside the table. */
  protected goToDecision(): void {
    this.tabChoice.set('run');
    afterNextRender(
      () => {
        const decision = document.getElementById('decision');
        const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        decision?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'nearest' });
        decision?.focus({ preventScroll: true });
      },
      { injector: this.injector },
    );
  }

  /**
   * Opens the reject form. It sits where the table ends, which may be below the screen while the decision bar is stuck
   * to the bottom, so bring it into view and put the cursor in the reason.
   */
  protected startReject(run: AllocationRun): void {
    this.rejecting.set(run.id);
    afterNextRender(
      () => {
        const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        document.querySelector('.reject-form')?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'center' });
        document.getElementById('reject-reason')?.focus({ preventScroll: true });
      },
      { injector: this.injector },
    );
  }

  protected pickTab(value: string | number | undefined): void {
    this.tabChoice.set(value as RoundTab);
  }

  /** Retry after a failed load. */
  protected reload(): void {
    this.refresh.update((n) => n + 1);
  }

  /** Some group won more or fewer seats than its quota (rollover). */
  protected offQuota(run: AllocationRun): boolean {
    return run.quota.some((q) => q.allocated !== q.base);
  }

  /** Outcomes of one kind in the run (the decision bar's tally). */
  protected countOf(run: AllocationRun, kind: OutcomeKind): number {
    return run.outcomes.filter((o) => o.kind === kind).length;
  }

  /** Locks in the draw: the ones won plus the ones left without a winner. */
  protected lockCount(run: AllocationRun): number {
    return this.countOf(run, 'Won') + run.unassignedLockCodes.length;
  }

  protected quotaOf(run: AllocationRun, category: Category): QuotaLine {
    return run.quota.find((q) => q.category === category) ?? { category, base: 0, allocated: 0 };
  }

  /** The weight column only earns its place when someone carries ×2 from the round before. */
  protected weighted(run: AllocationRun): boolean {
    return run.outcomes.some((o) => o.weight > 1);
  }

  protected pct(share: number): string {
    return `${Math.round(share * 100)}%`;
  }

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
  /** Run id whose approval is being confirmed in the dialog. */
  protected readonly approving = signal<string | null>(null);
  protected readonly deciding = signal(false);
  /** Run id whose reject form is open, and the reason typed so far. */
  protected readonly rejecting = signal<string | null>(null);
  protected readonly rejectReason = signal('');
  protected readonly checking = signal(false);
  /** p-table hands rows to its templates untyped; these restore the type for the template checker. */
  protected asRound(row: RoundSummary): RoundSummary {
    return row;
  }

  protected asRequestRow(row: RoundRequestRow): RoundRequestRow {
    return row;
  }

  protected asOutcome(row: OutcomeRow): OutcomeRow {
    return row;
  }

  /** Radio change for the review being answered; the literal keeps the outcome typed. */
  protected pickOutcome(outcome: ReviewOutcome): void {
    this.answering.update((a) => (a ? { ...a, outcome } : a));
  }

  protected readonly notice = signal<{ kind: 'ok' | 'error'; text: string } | null>(null);

  protected select(round: RoundSummary): void {
    this.notice.set(null);
    this.tabChoice.set(null);
    this.selectedId.set(round.id);
    // One column (under 1024px): the round opens below the list, so bring its heading into view
    if (window.innerWidth < 1024) {
      afterNextRender(() => document.getElementById('round-heading')?.scrollIntoView({ block: 'start' }), {
        injector: this.injector,
      });
    }
  }

  protected checkEligibility(round: RoundSummary): void {
    this.checking.set(true);
    this.notice.set(null);
    this.api.checkEligibility(round.id).subscribe({
      next: (rows) => {
        this.checking.set(false);
        const failed = rows.filter((r) => r.status === 'Ineligible').length;
        this.notice.set({ kind: 'ok', text: `ตรวจสิทธิแล้ว ${rows.length} คำขอ · ไม่ผ่าน ${failed}` });
        this.tabChoice.set('requests');
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
        this.tabChoice.set('run');
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
        this.approving.set(null);
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
    this.openAudit({ type: 'Round', id: round.id, label: `รอบ ${round.spaceName}` });
  }

  protected showRequestAudit(row: RoundRequestRow): void {
    this.openAudit({ type: 'StallRequest', id: row.requestId, label: `คำขอของ ${row.vendorName}` });
  }

  /** The approve button, so leaving the dialog puts the keyboard back on it (or on the run, once approved). */
  private approveOpener: HTMLElement | null = null;

  protected startApprove(run: AllocationRun): void {
    this.approveOpener = document.activeElement as HTMLElement | null;
    this.approving.set(run.id);
  }

  protected returnFromApprove(): void {
    const opener = this.approveOpener;
    this.approveOpener = null;
    afterNextRender(
      () => {
        // Approving removes the decision bar, so the run heading takes focus instead
        const target = opener?.isConnected ? opener : document.getElementById('run-heading');
        target?.focus({ preventScroll: true });
      },
      { injector: this.injector },
    );
  }

  /** The control that opened the audit panel, so closing it puts the keyboard back where the officer was. */
  private auditOpener: HTMLElement | null = null;

  private openAudit(target: { type: string; id: string; label: string }): void {
    if (this.auditTarget() === null) this.auditOpener = document.activeElement as HTMLElement | null;
    this.auditTarget.set(target);
  }

  protected focusAudit(): void {
    afterNextRender(() => document.getElementById('audit-heading')?.focus({ preventScroll: true }), {
      injector: this.injector,
    });
  }

  protected returnFromAudit(): void {
    this.auditOpener?.focus({ preventScroll: true });
    this.auditOpener = null;
  }
}

/** 0 a draw waits on approval · 1 requests wait on a check · 2 closed for requests · 3 still open. */
function urgency(r: RoundSummary): number {
  if (r.status === 'Drawn') return 0;
  if (r.requestCount > r.eligibleCount + r.ineligibleCount) return 1;
  return r.status === 'Closed' ? 2 : 3;
}

function load<T>(source: Observable<T>) {
  return source.pipe(
    map((data) => ({ state: 'ready', data }) as Load<T>),
    catchError(() => of({ state: 'error' } as Load<T>)),
    startWith({ state: 'loading' } as Load<T>),
  );
}

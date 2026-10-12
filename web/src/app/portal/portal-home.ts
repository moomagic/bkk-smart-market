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
import { Observable, catchError, combineLatest, map, of, scan, startWith, switchMap } from 'rxjs';
import { AllocationRun, Api, Category, InsightFlag, OutcomeKind, QuotaLine, ReviewOutcome, RoundRequestRow, RoundReview, RoundSummary, RunOutcome, problemMessage } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { ApproveRunDialog } from './approve-run-dialog';
import { RejectRunDialog } from './reject-run-dialog';
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
    RejectRunDialog,
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
    loadKeyed(toObservable(this.isOfficer), toObservable(this.refresh), (officer) =>
      officer ? load(this.api.rounds()) : of({ state: 'ready', data: [] } as Load<RoundSummary[]>),
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
    const steps = ['ยื่นคำขอ', 'ตรวจสิทธิ', 'จับฉลาก', 'อนุมัติผล'].map(
      (label, i): RoundStep => ({ label, state: i < current || current === 4 ? 'done' : i === current ? 'current' : 'todo' }),
    );
    // An approved round whose draw is not in the system (seeded history): the draw step says so instead of a tick
    if (this.noRunOnRecord()) steps[2] = { label: 'จับฉลาก', state: 'missing', note: 'ไม่มีข้อมูลการจับฉลากในระบบ' };
    return steps;
  });

  /** The runs have loaded and there are none, though the round is past its draw. */
  protected readonly noRunOnRecord = computed(() => {
    const runs = this.runs();
    const status = this.selected()?.status;
    return runs?.state === 'ready' && runs.data.length === 0 && (status === 'Drawn' || status === 'Approved');
  });

  protected readonly requests = toSignal(
    loadKeyed(toObservable(this.selectedId), toObservable(this.refresh), (id) => (id ? load(this.api.roundRequests(id)) : of(null))),
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
    loadKeyed(toObservable(this.selectedId), toObservable(this.refresh), (id) => (id ? load(this.api.allocationRuns(id)) : of(null))),
    { initialValue: null as Load<AllocationRun[]> | null },
  );

  protected readonly runList = computed(() => {
    const r = this.runs();
    return r?.state === 'ready' ? r.data : [];
  });

  /** Allocation Insight flags of the selected round (read-only). */
  protected readonly insights = toSignal(
    loadKeyed(toObservable(this.selectedId), toObservable(this.refresh), (id) => (id ? load(this.api.roundInsights(id)) : of(null))),
    { initialValue: null as Load<InsightFlag[]> | null },
  );

  protected readonly insightList = computed(() => {
    const r = this.insights();
    return r?.state === 'ready' ? r.data : [];
  });

  /** Some flag is more than a note, so the tab's count asks to be looked at. */
  protected readonly insightWarns = computed(() => this.insightList().some((f) => f.severity !== 'Info'));

  protected readonly reviews = toSignal(
    loadKeyed(toObservable(this.selectedId), toObservable(this.refresh), (id) => (id ? load(this.api.roundReviews(id)) : of(null))),
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
    if ((status === 'Drawn' || status === 'Approved') && !this.noRunOnRecord()) return 'run';
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
    // After a command, put the keyboard on what now carries the round (its heading, or the run's) once it is rendered;
    // the button that was pressed is disabled while busy, or gone once the round moves on
    afterRenderEffect(() => {
      const id = this.pendingFocus();
      this.runList();
      this.requestRows();
      if (!id) return;
      const target = document.getElementById(id);
      if (!target) return;
      target.focus({ preventScroll: true });
      untracked(() => this.pendingFocus.set(null));
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

  /** The round has a run to show, or will once the runs load (a drawn or approved round should have one). */
  protected readonly hasRun = computed(() => {
    const status = this.selected()?.status;
    return !!this.latestRun() || ((status === 'Drawn' || status === 'Approved') && !this.noRunOnRecord());
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
  /** Run id whose reject dialog is open. */
  protected readonly rejecting = signal<string | null>(null);
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
        this.pendingFocus.set('round-heading');
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
        this.pendingFocus.set('run-heading');
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.running.set(false);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  protected reject(run: AllocationRun, reason: string): void {
    if (!reason) return;
    this.deciding.set(true);
    this.notice.set(null);
    this.api.rejectAllocationRun(run.id, reason).subscribe({
      next: (r) => {
        this.deciding.set(false);
        this.decided = true;
        this.rejecting.set(null);
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
        this.decided = true;
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

  /** Element id to focus once it is rendered (see the constructor). */
  private readonly pendingFocus = signal<string | null>(null);

  /** The button that opened the approve or reject dialog, so leaving it puts the keyboard back there. */
  private dialogOpener: HTMLElement | null = null;

  protected startApprove(run: AllocationRun): void {
    this.dialogOpener = document.activeElement as HTMLElement | null;
    this.approving.set(run.id);
  }

  protected startReject(run: AllocationRun): void {
    this.dialogOpener = document.activeElement as HTMLElement | null;
    this.rejecting.set(run.id);
  }

  /** The dialog closed on a decision rather than a cancel. */
  private decided = false;

  /**
   * Once the dialog has gone: after a decision the run's heading takes focus (the decision bar is gone or changing);
   * left without deciding, the button that opened it does. Waiting for the close keeps the dialog's focus trap from
   * pulling focus back as it leaves.
   */
  protected returnFromDialog(): void {
    const opener = this.dialogOpener;
    this.dialogOpener = null;
    if (this.decided) {
      this.decided = false;
      this.pendingFocus.set('run-heading');
      return;
    }
    if (opener) afterNextRender(() => opener.focus({ preventScroll: true }), { injector: this.injector });
  }

  /** The control that opened the audit panel, so closing it puts the keyboard back where the officer was. */
  private auditOpener: HTMLElement | null = null;

  /** Trails the officer stepped through inside the panel (a request from the round, say), to step back to. */
  protected readonly auditTrail = signal<{ type: string; id: string; label: string }[]>([]);

  private openAudit(target: { type: string; id: string; label: string }): void {
    if (this.auditTarget() === null) this.auditOpener = document.activeElement as HTMLElement | null;
    this.auditTrail.set([]);
    this.auditTarget.set(target);
  }

  /** A change line in the panel names another entity: show its trail, remembering where the officer came from. */
  protected followAudit(target: { type: string; id: string; label: string }): void {
    const current = this.auditTarget();
    if (current) this.auditTrail.update((t) => [...t, current]);
    this.auditTarget.set(target);
    this.focusAudit();
  }

  protected backAudit(): void {
    const trail = this.auditTrail();
    const previous = trail.at(-1);
    if (!previous) return;
    this.auditTrail.set(trail.slice(0, -1));
    this.auditTarget.set(previous);
    this.focusAudit();
  }

  protected focusAudit(): void {
    afterNextRender(() => document.getElementById('audit-heading')?.focus({ preventScroll: true }), {
      injector: this.injector,
    });
  }

  protected returnFromAudit(): void {
    this.auditTrail.set([]);
    this.auditOpener?.focus({ preventScroll: true });
    this.auditOpener = null;
  }

  /**
   * The panel is fixed to the screen but the site header scrolls away, so its top follows the header's bottom edge
   * (60px at the top of the page, 0 once the header has gone) instead of leaving a band of page above it.
   */
  private readonly keepAuditUnderHeader = effect((onCleanup) => {
    if (this.auditTarget() === null) return;
    const root = document.documentElement;
    const place = () => {
      const bottom = document.querySelector('header.bar')?.getBoundingClientRect().bottom ?? 0;
      root.style.setProperty('--audit-top', `${Math.max(0, Math.round(bottom))}px`);
    };
    place();
    window.addEventListener('scroll', place, { passive: true });
    onCleanup(() => {
      window.removeEventListener('scroll', place);
      root.style.removeProperty('--audit-top');
    });
  });
}

/** 0 a draw waits on approval · 1 requests wait on a check · 2 closed for requests · 3 still open. */
function urgency(r: RoundSummary): number {
  if (r.status === 'Drawn') return 0;
  if (r.requestCount > r.eligibleCount + r.ineligibleCount) return 1;
  return r.status === 'Closed' ? 2 : 3;
}

/**
 * Loads for a key (the selected round, or the role) and again on every refresh. A refresh of the same key keeps the
 * data it has on screen until the new data arrives: the page does not flash "กำลังโหลด…" after a command, and what
 * holds the keyboard focus is not torn down underneath it. A new key, or a retry after an error, shows loading.
 */
function loadKeyed<K, L extends { state: string } | null>(
  key$: Observable<K>,
  refresh$: Observable<number>,
  fetch: (key: K) => Observable<L>,
): Observable<L> {
  return combineLatest([key$, refresh$]).pipe(
    switchMap(([key]) => fetch(key).pipe(map((value) => ({ key, value })))),
    scan((shown: { key: K; value: L } | null, next) =>
      shown && shown.key === next.key && shown.value?.state === 'ready' && next.value?.state === 'loading' ? shown : next,
    null),
    map((s) => s!.value),
  );
}

function load<T>(source: Observable<T>) {
  return source.pipe(
    map((data) => ({ state: 'ready', data }) as Load<T>),
    catchError(() => of({ state: 'error' } as Load<T>)),
    startWith({ state: 'loading' } as Load<T>),
  );
}

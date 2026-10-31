import { DestroyRef, Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { Router } from '@angular/router';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, Subscription, catchError, combineLatest, map, of, scan, startWith, switchMap } from 'rxjs';
import { Api, AssistantAnswer, AssistantSource, PreferredTime, RunVerification, SpaceMatch, SpaceSummary, StallRequest, StallRequestStatus, lockQr, problemMessage } from '../core/api';
import { DemoRole, DemoSession } from '../core/demo-session';
import { deviceId } from '../core/device-id';
import { CATEGORY_LABEL, STATUS_LABEL, formatThaiDateTime } from '../shared/labels';

const FINISHED = new Set<StallRequestStatus>(['NotSelected', 'Forfeited']);
/** Over as far as applying again goes (ADR-0003): Ineligible too, though its card stays among the current ones. */
const CLOSED = new Set<StallRequestStatus>(['Ineligible', 'NotSelected', 'Forfeited']);

export type Load<T> = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: T };
export type Notice = { kind: 'ok' | 'warn' | 'error'; text: string };
export type ChatMessage = {
  from: 'me' | 'assistant';
  text: string;
  found?: boolean;
  source?: AssistantSource;
  /** The rules sections the answer used, as the assistant returned them. */
  citations?: AssistantAnswer['citations'];
  /** The question never reached an answer (network or server problem): the text says why. */
  failed?: boolean;
};

/**
 * The vendor area's data and actions, shared by every screen in it: the spaces, the vendor's own requests, the
 * notice after an action, and the actions themselves. Provided once per area so the screens see the same state.
 */
@Injectable()
export class VendorStore {
  private readonly api = inject(Api);
  private readonly session = inject(DemoSession);
  private readonly router = inject(Router);
  /** In-flight calls end with the vendor app (the shell provides this store). */
  private readonly destroyRef = inject(DestroyRef);

  /** Bumped after an action so both lists reload. */
  private readonly refresh = signal(0);
  /**
   * Each load, and whether it only refreshes what is already on screen (same role). A refresh keeps the current
   * data until the new data arrives, so the screen the vendor just acted on keeps its buttons, focus and scroll;
   * a role switch starts again from "กำลังโหลด…".
   */
  private readonly reload$ = combineLatest([toObservable(this.session.role), toObservable(this.refresh)]).pipe(
    scan((prev: Reload | null, [role]) => ({ role, quiet: prev?.role === role }), null),
    map((r) => r!),
  );

  readonly spaces = toSignal(
    this.reload$.pipe(switchMap(({ quiet }) => loadState(this.api.spaces(), quiet))),
    { initialValue: { state: 'loading' } as Load<SpaceSummary[]> },
  );

  readonly spaceList = computed(() => {
    const s = this.spaces();
    return s.state === 'ready' ? s.data : [];
  });

  private readonly fetchedRequests = toSignal(
    this.reload$.pipe(
      switchMap(({ role, quiet }) =>
        role === 'vendor' ? loadState(this.api.myStallRequests(), quiet) : of(null),
      ),
    ),
    { initialValue: null as Load<StallRequest[]> | null },
  );

  /** The vendor's requests as fetched; a submit adds its new request at once, until the next fetch replaces it. */
  readonly myRequests = linkedSignal(() => this.fetchedRequests());

  private readonly requestList = computed(() => {
    const mine = this.myRequests();
    return mine?.state === 'ready' ? mine.data : [];
  });

  /** Requests still in play lead; a draw that went elsewhere or a forfeited lock folds under "คำขอที่จบแล้ว". */
  readonly current = computed(() => this.requestList().filter((r) => !FINISHED.has(r.status)));
  readonly finished = computed(() => this.requestList().filter((r) => FINISHED.has(r.status)));

  /** Won locks waiting on the vendor's confirmation, the nearest deadline first: the one thing with a clock on it. */
  readonly awaitingConfirm = computed(() =>
    this.current()
      .filter((r) => r.status === 'Won' && r.confirmBy)
      .sort((a, b) => a.confirmBy!.localeCompare(b.confirmBy!)),
  );

  /**
   * A round is open and the vendor may apply to it: they hold no unfinished request in a round whose period
   * overlaps it (one person, one open request across the district — ADR-0003; Ineligible, NotSelected and
   * Forfeited are finished). The server still decides; this only says when to offer it.
   */
  readonly canApplyNow = computed(() => {
    if (this.session.role() !== 'vendor') return false;
    const open = this.spaceList().flatMap((s) => (s.openRound ? [s.openRound] : []));
    const holding = this.requestList().filter((r) => !CLOSED.has(r.status));
    return open.some(
      (round) => !holding.some((r) => r.roundStartsOn <= round.endsOn && r.roundEndsOn >= round.startsOn),
    );
  });

  /** A won lock is waiting on the vendor: the requests tab carries a dot. */
  readonly hasTodo = computed(() => this.awaitingConfirm().length > 0);

  /** The vendor's request status in each round they applied to. */
  readonly requestStatusByRound = computed(
    () => new Map(this.requestList().map((r) => [r.roundId, r.status])),
  );

  /** A matched space the vendor can submit to right now, for the "ยื่นคำขอที่นี่" button. */
  readonly submittable = computed(() => {
    const spaces = this.spaceList();
    const requested = this.requestStatusByRound();
    const isVendor = this.session.role() === 'vendor';
    return (spaceId: string): SpaceSummary | null => {
      const space = spaces.find((s) => s.id === spaceId);
      return isVendor && space?.openRound && !requested.has(space.openRound.id) ? space : null;
    };
  });

  /** The status of the vendor's request in a space's open round, or null when they have none there. */
  readonly requestStatus = computed(() => {
    const spaces = this.spaceList();
    const requested = this.requestStatusByRound();
    return (spaceId: string): StallRequestStatus | null => {
      const round = spaces.find((s) => s.id === spaceId)?.openRound;
      return (round && requested.get(round.id)) ?? null;
    };
  });

  /** The one apply-by date of every open round, when they share it; otherwise each space states its own. */
  readonly closesAt = computed(() => {
    const dates = new Set(this.spaceList().flatMap((s) => (s.openRound ? [s.openRound.applyClosesAt] : [])));
    return dates.size === 1 ? [...dates][0] : null;
  });

  /**
   * Space Matching's last search and its matches. Kept here, not in the screen, so a vendor who submits from a
   * match and comes back with ← finds the search where they left it. Null matches: no search shown, the full list is.
   */
  readonly match = {
    product: signal(''),
    area: signal(''),
    time: signal<PreferredTime | null>(null),
    results: signal<SpaceMatch[] | null>(null),
    busy: signal(false),
    error: signal<string | null>(null),
  };
  /** Matches stand in for the full list only when there are some; "not found" keeps the list in view. */
  readonly showingMatches = computed(() => (this.match.results()?.length ?? 0) > 0);
  private matchCall?: Subscription;

  /** Runs Space Matching on the current search; a newer search drops the one still in flight. */
  findMatches(): void {
    const m = this.match;
    this.matchCall?.unsubscribe();
    m.busy.set(true);
    m.error.set(null);
    this.matchCall = this.api.matchSpaces(m.product().trim(), m.area().trim(), m.time()).subscribe({
      next: (r) => {
        m.busy.set(false);
        m.results.set(r);
      },
      error: (err) => {
        m.busy.set(false);
        // A failed search shows no matches, not the previous search's
        m.results.set(null);
        m.error.set(problemMessage(err));
      },
    });
  }

  /** Back to the full list; the search stays in the form. */
  clearMatches(): void {
    this.match.results.set(null);
  }

  /**
   * The Vendor Assistant conversation, kept for the life of the vendor app (memory only, so a demo reset or a
   * reload starts clean), so leaving the chat for another tab and coming back finds it as it was.
   */
  readonly assistant = {
    messages: signal<ChatMessage[]>([]),
    draft: signal(''),
    busy: signal(false),
  };

  /** The vendor app's scrolling screen, set by the shell: the one element a screen may scroll itself. */
  readonly scroller = signal<HTMLElement | null>(null);

  /**
   * Asks the Vendor Assistant; the answer, or why there is none, joins the conversation. `fromDraft` when the
   * question is the one typed in the box, which then empties; a suggestion leaves the box as it was.
   */
  ask(question: string, fromDraft = false): void {
    const a = this.assistant;
    const q = question.trim();
    if (!q || a.busy()) return;
    a.busy.set(true);
    if (fromDraft) a.draft.set('');
    a.messages.update((m) => [...m, { from: 'me', text: q }]);
    this.api.askAssistant(q).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (ans) => {
        a.busy.set(false);
        a.messages.update((m) => [...m, { from: 'assistant', text: ans.text, found: ans.found, source: ans.source, citations: ans.citations }]);
      },
      error: (err) => {
        a.busy.set(false);
        a.messages.update((m) => [...m, { from: 'assistant', text: problemMessage(err), found: false, failed: true }]);
      },
    });
  }

  /** Space id with a submit in flight. */
  readonly submitting = signal<string | null>(null);
  /** Request id with a confirm/forfeit/check-in/review in flight. */
  readonly acting = signal<string | null>(null);
  /** Demo toggle: pretend the phone is outside the space when scanning. */
  readonly outsideGeofence = signal(false);
  readonly notice = signal<Notice | null>(null);

  /** Retry after a failed load. */
  reload(): void {
    this.refresh.update((n) => n + 1);
  }

  /**
   * Submits to the space's open round, then opens the new request on its own screen: what it is now and what
   * happens next. The notice is set after the navigation, which clears the previous screen's.
   */
  submit(space: SpaceSummary): void {
    const round = space.openRound;
    if (!round) return;
    this.submitting.set(space.id);
    this.notice.set(null);
    this.api.submitStallRequest(round.id).subscribe({
      next: async (req) => {
        this.submitting.set(null);
        this.myRequests.update((m) => (m?.state === 'ready' ? { state: 'ready', data: [...m.data, req] } : m));
        this.reload();
        await this.router.navigate(['/vendor/requests', req.id]);
        this.notice.set({
          kind: 'ok',
          text: `${STATUS_LABEL[req.status]} · ${req.spaceName} · กลุ่ม${CATEGORY_LABEL[req.category]} — ยังไม่ใช่การได้สิทธิ์ ผลจับฉลากประกาศหลังปิดรับคำขอ`,
        });
      },
      error: (err) => {
        this.submitting.set(null);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  confirm(req: StallRequest): Promise<boolean> {
    return this.act(req, this.api.confirmStallRequest(req.id), (r) => `ยืนยันสิทธิ์แล้ว · ${r.spaceName} ล็อค ${r.lockCode}`);
  }

  forfeit(req: StallRequest): Promise<boolean> {
    return this.act(req, this.api.forfeitStallRequest(req.id), (r) => `สละสิทธิ์แล้ว · ${r.spaceName} — ล็อคส่งต่อให้ผู้สำรองตามลำดับ`);
  }

  /** Simulates scanning the QR posted at the vendor's own lock. */
  checkIn(req: StallRequest): void {
    if (!req.lockCode) return;
    this.acting.set(req.id);
    this.notice.set(null);
    this.api.checkIn(req.id, lockQr(req.spaceId, req.lockCode), !this.outsideGeofence(), deviceId()).subscribe({
      next: (c) => {
        this.acting.set(null);
        this.notice.set({
          kind: c.withinGeofence ? 'ok' : 'warn',
          text: c.withinGeofence
            ? `เช็คอินแล้ว · ${c.spaceName} ล็อค ${c.lockCode} · ${formatThaiDateTime(c.at)}`
            : `เช็คอินแล้ว แต่ตำแหน่งอยู่นอกพื้นที่ — บันทึกไว้ให้เจ้าหน้าที่ตรวจสอบ`,
        });
        this.reload();
      },
      error: (err) => {
        this.acting.set(null);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  /** Resolves true once the review is filed. */
  fileReview(req: StallRequest, reason: string): Promise<boolean> {
    return this.act(req, this.api.fileReview(req.id, reason), () => `ยื่นขอทบทวนผลแล้ว · ${req.spaceName} — รอเขตตอบ`, false);
  }

  /** Replay verification of one run of the vendor's round: a read, so no notice, no reload. */
  verifyRun(runId: string): Observable<RunVerification> {
    return this.api.verifyAllocationRun(runId);
  }

  /** Runs one request action; true when it went through. A failed confirm/forfeit reloads, as the state may have moved. */
  private act<T>(
    req: StallRequest,
    call: Observable<T>,
    message: (r: T) => string,
    reloadOnError = true,
  ): Promise<boolean> {
    this.acting.set(req.id);
    this.notice.set(null);
    return new Promise((resolve) => {
      call.subscribe({
        next: (r) => {
          this.acting.set(null);
          this.notice.set({ kind: 'ok', text: message(r) });
          this.reload();
          resolve(true);
        },
        error: (err) => {
          this.acting.set(null);
          this.notice.set({ kind: 'error', text: problemMessage(err) });
          if (reloadOnError) this.reload();
          resolve(false);
        },
      });
    });
  }
}

type Reload = { role: DemoRole; quiet: boolean };

/** Loading, then the data or an error; a quiet refresh skips the loading state. */
function loadState<T>(source: Observable<T>, quiet: boolean): Observable<Load<T>> {
  const loaded = source.pipe(
    map((data) => ({ state: 'ready', data }) as Load<T>),
    catchError(() => of({ state: 'error' } as Load<T>)),
  );
  return quiet ? loaded : loaded.pipe(startWith({ state: 'loading' } as Load<T>));
}

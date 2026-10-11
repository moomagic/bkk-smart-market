import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { Router } from '@angular/router';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, combineLatest, map, of, scan, startWith, switchMap } from 'rxjs';
import { Api, SpaceSummary, StallRequest, StallRequestStatus, lockQr, problemMessage } from '../core/api';
import { DemoRole, DemoSession } from '../core/demo-session';
import { CATEGORY_LABEL, STATUS_LABEL, formatThaiDateTime } from '../shared/labels';

const FINISHED = new Set<StallRequestStatus>(['NotSelected', 'Forfeited']);
/** Over as far as applying again goes (ADR-0003): Ineligible too, though its card stays among the current ones. */
const CLOSED = new Set<StallRequestStatus>(['Ineligible', 'NotSelected', 'Forfeited']);

export type Load<T> = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: T };
export type Notice = { kind: 'ok' | 'warn' | 'error'; text: string };

/**
 * The vendor area's data and actions, shared by every screen in it: the spaces, the vendor's own requests, the
 * notice after an action, and the actions themselves. Provided once per area so the screens see the same state.
 */
@Injectable()
export class VendorStore {
  private readonly api = inject(Api);
  private readonly session = inject(DemoSession);
  private readonly router = inject(Router);

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
    this.api.checkIn(req.id, lockQr(req.spaceId, req.lockCode), !this.outsideGeofence()).subscribe({
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

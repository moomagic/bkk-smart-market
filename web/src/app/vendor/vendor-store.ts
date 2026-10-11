import { Injectable, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { Api, SpaceSummary, StallRequest, StallRequestStatus, lockQr, problemMessage } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { CATEGORY_LABEL, STATUS_LABEL, formatThaiDateTime } from '../shared/labels';

const FINISHED = new Set<StallRequestStatus>(['NotSelected', 'Forfeited']);

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

  /** Bumped after an action so both lists reload. */
  private readonly refresh = signal(0);
  private readonly reload$ = combineLatest([toObservable(this.session.role), toObservable(this.refresh)]);

  readonly spaces = toSignal(
    this.reload$.pipe(switchMap(() => loadState(this.api.spaces()))),
    { initialValue: { state: 'loading' } as Load<SpaceSummary[]> },
  );

  readonly spaceList = computed(() => {
    const s = this.spaces();
    return s.state === 'ready' ? s.data : [];
  });

  readonly myRequests = toSignal(
    this.reload$.pipe(
      switchMap(([role]) => (role === 'vendor' ? loadState(this.api.myStallRequests()) : of(null))),
    ),
    { initialValue: null as Load<StallRequest[]> | null },
  );

  private readonly requestList = computed(() => {
    const mine = this.myRequests();
    return mine?.state === 'ready' ? mine.data : [];
  });

  /** Requests still in play lead; a draw that went elsewhere or a forfeited lock folds under "คำขอที่จบแล้ว". */
  readonly current = computed(() => this.requestList().filter((r) => !FINISHED.has(r.status)));
  readonly finished = computed(() => this.requestList().filter((r) => FINISHED.has(r.status)));

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

  /** Submits to the space's open round; resolves with the new request, or null when it failed (the notice says why). */
  submit(space: SpaceSummary): Promise<StallRequest | null> {
    const round = space.openRound;
    if (!round) return Promise.resolve(null);
    this.submitting.set(space.id);
    this.notice.set(null);
    return new Promise((resolve) => {
      this.api.submitStallRequest(round.id).subscribe({
        next: (req) => {
          this.submitting.set(null);
          this.notice.set({
            kind: 'ok',
            text: `${STATUS_LABEL[req.status]} · ${req.spaceName} · กลุ่ม${CATEGORY_LABEL[req.category]} — ยังไม่ใช่การได้สิทธิ์ ผลจับฉลากประกาศหลังปิดรับคำขอ`,
          });
          this.reload();
          resolve(req);
        },
        error: (err) => {
          this.submitting.set(null);
          this.notice.set({ kind: 'error', text: problemMessage(err) });
          resolve(null);
        },
      });
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

function loadState<T>(source: Observable<T>) {
  return source.pipe(
    map((data) => ({ state: 'ready', data }) as Load<T>),
    catchError(() => of({ state: 'error' } as Load<T>)),
    startWith({ state: 'loading' } as Load<T>),
  );
}
